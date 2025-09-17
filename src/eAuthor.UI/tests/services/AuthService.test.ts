import { describe, it, expect, beforeEach, vi, Mock } from 'vitest';
import { Container } from 'inversify';
import { AuthService } from './../../src/services/AuthService';
import { TYPES, OAuthConfig, ITokenManager, IHttpClient } from './../../src/types';
import axios from 'axios';

// Mock axios
vi.mock('axios');
const mockedAxios = axios as any;

// Mock sessionStorage
const mockSessionStorage = {
    getItem: vi.fn(),
    setItem: vi.fn(),
    removeItem: vi.fn(),
    clear: vi.fn()
};
Object.defineProperty(window, 'sessionStorage', {
    value: mockSessionStorage
});

describe('AuthService', () => {
    let container: Container;
    let authService: AuthService;
    let mockTokenManager: any;
    let mockHttpClient: any;

    const mockOAuthConfig: OAuthConfig = {
        clientId: 'test-client-id',
        redirectUri: 'http://localhost:3000/auth/callback',
        scope: 'read:user',
        authUrl: 'https://github.com/login/oauth/authorize',
        tokenUrl: 'https://github.com/login/oauth/access_token',
        userInfoUrl: 'https://api.github.com/user'
    };

    beforeEach(() => {
        vi.clearAllMocks();

        container = new Container();

        mockTokenManager = {
            getAccessToken: vi.fn(),
            getRefreshToken: vi.fn(),
            setTokens: vi.fn(),
            clearTokens: vi.fn(),
            isTokenExpired: vi.fn(),
            refreshAccessToken: vi.fn(),
            getTokenType: vi.fn()
        };

        mockHttpClient = {
            get: vi.fn(),
            post: vi.fn(),
            put: vi.fn(),
            delete: vi.fn(),
            setBaseUrl: vi.fn(),
            getBaseUrl: vi.fn()
        };

        container.bind(TYPES.TokenManager).toConstantValue(mockTokenManager);
        container.bind(TYPES.HttpClient).toConstantValue(mockHttpClient);
        container.bind(TYPES.OAuthConfig).toConstantValue(mockOAuthConfig);
        container.bind(TYPES.AuthService).to(AuthService);

        authService = container.get<AuthService>(TYPES.AuthService);

        // Mock axios
        mockedAxios.post = vi.fn();
        mockSessionStorage.setItem.mockClear();
        mockSessionStorage.getItem.mockClear();
        mockSessionStorage.removeItem.mockClear();
    });

    describe('getAuthUrl', () => {
        it('should generate correct authorization URL', () => {
            const authUrl = authService.getAuthUrl();

            expect(authUrl).toContain(mockOAuthConfig.authUrl);
            expect(authUrl).toContain(`client_id=${mockOAuthConfig.clientId}`);
            expect(authUrl).toContain(`redirect_uri=${encodeURIComponent(mockOAuthConfig.redirectUri)}`);
            expect(authUrl).toContain(`scope=${mockOAuthConfig.scope}`);
            expect(authUrl).toContain('response_type=code');
            expect(authUrl).toContain('state=');
        });

        it('should generate and store state parameter', () => {
            const authUrl = authService.getAuthUrl();

            expect(mockSessionStorage.setItem).toHaveBeenCalledWith('oauth_state', expect.any(String));

            const stateParam = new URL(authUrl).searchParams.get('state');
            expect(stateParam).toBeTruthy();
            expect(stateParam!.length).toBeGreaterThan(10);
        });
    });

    describe('login', () => {
        it('should successfully login with authorization code', async () => {
            const mockTokenResponse = {
                access_token: 'access_token',
                refresh_token: 'refresh_token',
                expires_in: 3600,
                token_type: 'Bearer'
            };

            const mockGitHubUser = {
                id: 123,
                login: 'testuser',
                name: 'Test User',
                email: 'test@example.com',
                avatar_url: 'https://github.com/images/error/octocat_happy.gif'
            };

            const expectedUser = {
                id: '123',
                name: 'Test User',
                email: 'test@example.com',
                avatar: 'https://github.com/images/error/octocat_happy.gif'
            };

            mockedAxios.post.mockResolvedValueOnce({ data: mockTokenResponse });
            mockHttpClient.get.mockResolvedValueOnce({ data: mockGitHubUser, status: 200 });

            const result = await authService.login('test_code');

            expect(mockedAxios.post).toHaveBeenCalledWith(
                mockOAuthConfig.tokenUrl,
                expect.objectContaining({
                    client_id: mockOAuthConfig.clientId,
                    code: 'test_code',
                    redirect_uri: mockOAuthConfig.redirectUri,
                    grant_type: 'authorization_code'
                }),
                expect.objectContaining({
                    headers: {
                        'Accept': 'application/json',
                        'Content-Type': 'application/json'
                    }
                })
            );

            expect(mockTokenManager.setTokens).toHaveBeenCalledWith({
                accessToken: mockTokenResponse.access_token,
                refreshToken: mockTokenResponse.refresh_token,
                expiresIn: mockTokenResponse.expires_in,
                tokenType: mockTokenResponse.token_type
            });

            expect(mockHttpClient.get).toHaveBeenCalledWith(mockOAuthConfig.userInfoUrl);
            expect(result).toEqual(expectedUser);
        });

        it('should handle missing user email', async () => {
            const mockTokenResponse = {
                access_token: 'access_token',
                refresh_token: 'refresh_token',
                expires_in: 3600,
                token_type: 'Bearer'
            };

            const mockGitHubUser = {
                id: 123,
                login: 'testuser',
                name: 'Test User'
                // email is missing
            };

            const expectedUser = {
                id: '123',
                name: 'Test User',
                email: '', // Should default to empty string
                avatar: undefined
            };

            mockedAxios.post.mockResolvedValueOnce({ data: mockTokenResponse });
            mockHttpClient.get.mockResolvedValueOnce({ data: mockGitHubUser, status: 200 });

            const result = await authService.login('test_code');

            expect(result).toEqual(expectedUser);
        });

        it('should use login as fallback for missing name and id', async () => {
            const mockTokenResponse = {
                access_token: 'access_token',
                refresh_token: 'refresh_token',
                expires_in: 3600,
                token_type: 'Bearer'
            };

            const mockGitHubUser = {
                login: 'testuser'
                // id and name are missing
            };

            const expectedUser = {
                id: 'testuser', // Should fallback to login
                name: 'testuser', // Should fallback to login
                email: '',
                avatar: undefined
            };

            mockedAxios.post.mockResolvedValueOnce({ data: mockTokenResponse });
            mockHttpClient.get.mockResolvedValueOnce({ data: mockGitHubUser, status: 200 });

            const result = await authService.login('test_code');

            expect(result).toEqual(expectedUser);
        });

        it('should handle missing token_type in response', async () => {
            const mockTokenResponse = {
                access_token: 'access_token',
                refresh_token: 'refresh_token',
                expires_in: 3600
                // token_type is missing
            };

            const mockGitHubUser = {
                id: 123,
                login: 'testuser',
                name: 'Test User',
                email: 'test@example.com'
            };

            mockedAxios.post.mockResolvedValueOnce({ data: mockTokenResponse });
            mockHttpClient.get.mockResolvedValueOnce({ data: mockGitHubUser, status: 200 });

            await authService.login('test_code');

            expect(mockTokenManager.setTokens).toHaveBeenCalledWith({
                accessToken: mockTokenResponse.access_token,
                refreshToken: mockTokenResponse.refresh_token,
                expiresIn: mockTokenResponse.expires_in,
                tokenType: 'Bearer' // Should default to 'Bearer'
            });
        });

        it('should throw error when token exchange fails', async () => {
            mockedAxios.post.mockRejectedValueOnce(new Error('Token exchange failed'));

            await expect(authService.login('invalid_code')).rejects.toThrow('Login failed');
            expect(mockTokenManager.setTokens).not.toHaveBeenCalled();
        });

        it('should throw error when user info fetch fails', async () => {
            const mockTokenResponse = {
                access_token: 'access_token',
                refresh_token: 'refresh_token',
                expires_in: 3600,
                token_type: 'Bearer'
            };

            mockedAxios.post.mockResolvedValueOnce({ data: mockTokenResponse });
            mockHttpClient.get.mockRejectedValueOnce(new Error('User info failed'));

            await expect(authService.login('test_code')).rejects.toThrow('Login failed');
            expect(mockTokenManager.setTokens).toHaveBeenCalled();
        });
    });

    describe('logout', () => {
        it('should clear tokens on logout', async () => {
            await authService.logout();
            expect(mockTokenManager.clearTokens).toHaveBeenCalled();
        });
    });

    describe('refreshToken', () => {
        it('should delegate to token manager', async () => {
            const newTokens = {
                accessToken: 'new_access_token',
                refreshToken: 'new_refresh_token',
                expiresIn: 3600,
                tokenType: 'Bearer'
            };

            mockTokenManager.refreshAccessToken.mockResolvedValueOnce(newTokens);

            const result = await authService.refreshToken();

            expect(mockTokenManager.refreshAccessToken).toHaveBeenCalled();
            expect(result).toEqual(newTokens);
        });
    });

    describe('getCurrentUser', () => {
        it('should fetch current user info', async () => {
            const mockGitHubUser = {
                id: 123,
                login: 'testuser',
                name: 'Test User',
                email: 'test@example.com',
                avatar_url: 'https://github.com/images/error/octocat_happy.gif'
            };

            const expectedUser = {
                id: '123',
                name: 'Test User',
                email: 'test@example.com',
                avatar: 'https://github.com/images/error/octocat_happy.gif'
            };

            mockHttpClient.get.mockResolvedValueOnce({ data: mockGitHubUser, status: 200 });

            const result = await authService.getCurrentUser();

            expect(mockHttpClient.get).toHaveBeenCalledWith(mockOAuthConfig.userInfoUrl);
            expect(result).toEqual(expectedUser);
        });

        it('should throw error when user info fetch fails', async () => {
            mockHttpClient.get.mockRejectedValueOnce(new Error('Unauthorized'));

            await expect(authService.getCurrentUser()).rejects.toThrow('Failed to get user info');
        });
    });

    describe('validateState', () => {
        it('should return true for valid state', () => {
            const testState = 'test_state_123';
            mockSessionStorage.getItem.mockReturnValue(testState);

            const result = authService.validateState(testState);

            expect(mockSessionStorage.getItem).toHaveBeenCalledWith('oauth_state');
            expect(mockSessionStorage.removeItem).toHaveBeenCalledWith('oauth_state');
            expect(result).toBe(true);
        });

        it('should return false for invalid state', () => {
            const storedState = 'stored_state';
            const providedState = 'different_state';
            mockSessionStorage.getItem.mockReturnValue(storedState);

            const result = authService.validateState(providedState);

            expect(result).toBe(false);
        });

        it('should return false when no stored state', () => {
            mockSessionStorage.getItem.mockReturnValue(null);

            const result = authService.validateState('some_state');

            expect(result).toBe(false);
        });
    });

    describe('isAuthenticated', () => {
        it('should return true when user has valid token', () => {
            mockTokenManager.getAccessToken.mockReturnValue('valid_token');
            mockTokenManager.isTokenExpired.mockReturnValue(false);

            const result = authService.isAuthenticated();

            expect(result).toBe(true);
        });

        it('should return false when user has no token', () => {
            mockTokenManager.getAccessToken.mockReturnValue(null);

            const result = authService.isAuthenticated();

            expect(result).toBe(false);
        });

        it('should return false when token is expired', () => {
            mockTokenManager.getAccessToken.mockReturnValue('expired_token');
            mockTokenManager.isTokenExpired.mockReturnValue(true);

            const result = authService.isAuthenticated();

            expect(result).toBe(false);
        });
    });
});