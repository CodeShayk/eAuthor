import { describe, it, expect, beforeEach, vi, Mock } from 'vitest';
import { Container } from 'inversify';
import { TokenManager } from '@/services/TokenManager';
import { TYPES, AuthTokens } from '@/types';
import axios from 'axios';

// Mock axios
vi.mock('axios');
const mockedAxios = axios as any;

describe('TokenManager', () => {
    let container: Container;
    let tokenManager: TokenManager;

    const mockTokens: AuthTokens = {
        accessToken: 'access_token_123',
        refreshToken: 'refresh_token_123',
        expiresIn: 3600,
        tokenType: 'Bearer'
    };

    beforeEach(() => {
        vi.clearAllMocks();
        localStorage.clear();

        container = new Container();
        container.bind<TokenManager>(TYPES.TokenManager).to(TokenManager);
        tokenManager = container.get<TokenManager>(TYPES.TokenManager);

        // Reset axios mock
        mockedAxios.post = vi.fn();
    });

    describe('setTokens and getTokens', () => {
        it('should store and retrieve access token', () => {
            tokenManager.setTokens(mockTokens);

            expect(tokenManager.getAccessToken()).toBe(mockTokens.accessToken);
            expect(tokenManager.getRefreshToken()).toBe(mockTokens.refreshToken);
            expect(tokenManager.getTokenType()).toBe(mockTokens.tokenType);
        });

        it('should return null when no tokens are stored', () => {
            expect(tokenManager.getAccessToken()).toBeNull();
            expect(tokenManager.getRefreshToken()).toBeNull();
        });

        it('should store expiration time correctly', () => {
            const beforeTime = Date.now();
            tokenManager.setTokens(mockTokens);
            const afterTime = Date.now();

            const storedExpiration = localStorage.getItem('oauth_expires_at');
            expect(storedExpiration).toBeTruthy();

            const expirationTime = parseInt(storedExpiration!, 10);
            const expectedMinTime = beforeTime + (mockTokens.expiresIn * 1000);
            const expectedMaxTime = afterTime + (mockTokens.expiresIn * 1000);

            expect(expirationTime).toBeGreaterThanOrEqual(expectedMinTime);
            expect(expirationTime).toBeLessThanOrEqual(expectedMaxTime);
        });
    });

    describe('clearTokens', () => {
        it('should remove all stored tokens', () => {
            tokenManager.setTokens(mockTokens);

            // Verify tokens are stored
            expect(localStorage.getItem('oauth_access_token')).toBe(mockTokens.accessToken);
            expect(localStorage.getItem('oauth_refresh_token')).toBe(mockTokens.refreshToken);
            expect(localStorage.getItem('oauth_expires_at')).toBeTruthy();
            expect(localStorage.getItem('oauth_token_type')).toBe(mockTokens.tokenType);

            tokenManager.clearTokens();

            expect(tokenManager.getAccessToken()).toBeNull();
            expect(tokenManager.getRefreshToken()).toBeNull();
            expect(localStorage.getItem('oauth_access_token')).toBeNull();
            expect(localStorage.getItem('oauth_refresh_token')).toBeNull();
            expect(localStorage.getItem('oauth_expires_at')).toBeNull();
            expect(localStorage.getItem('oauth_token_type')).toBeNull();
        });
    });

    describe('isTokenExpired', () => {
        it('should return false for valid token', () => {
            tokenManager.setTokens(mockTokens);

            expect(tokenManager.isTokenExpired()).toBe(false);
        });

        it('should return true when no expiration time is stored', () => {
            expect(tokenManager.isTokenExpired()).toBe(true);
        });

        it('should return true for expired token', () => {
            const expiredTokens = { ...mockTokens, expiresIn: -7200 }; // Expired 2 hours ago
            tokenManager.setTokens(expiredTokens);

            expect(tokenManager.isTokenExpired()).toBe(true);
        });

        it('should consider token expired 5 minutes before actual expiration', () => {
            const almostExpiredTokens = { ...mockTokens, expiresIn: 240 }; // 4 minutes from now
            tokenManager.setTokens(almostExpiredTokens);

            expect(tokenManager.isTokenExpired()).toBe(true);
        });

        it('should not consider token expired if more than 5 minutes remain', () => {
            const validTokens = { ...mockTokens, expiresIn: 600 }; // 10 minutes from now
            tokenManager.setTokens(validTokens);

            expect(tokenManager.isTokenExpired()).toBe(false);
        });
    });

    describe('refreshAccessToken', () => {
        it('should successfully refresh tokens', async () => {
            const newTokens = {
                access_token: 'new_access_token',
                refresh_token: 'new_refresh_token',
                expires_in: 3600,
                token_type: 'Bearer'
            };

            // Set up initial tokens
            tokenManager.setTokens(mockTokens);

            // Mock successful axios response
            mockedAxios.post.mockResolvedValueOnce({ data: newTokens });

            const result = await tokenManager.refreshAccessToken();

            expect(mockedAxios.post).toHaveBeenCalledWith(
                expect.stringContaining('oauth/access_token'),
                expect.objectContaining({
                    grant_type: 'refresh_token',
                    refresh_token: mockTokens.refreshToken
                }),
                expect.objectContaining({
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json'
                    }
                })
            );

            expect(result.accessToken).toBe(newTokens.access_token);
            expect(result.refreshToken).toBe(newTokens.refresh_token);
            expect(result.expiresIn).toBe(newTokens.expires_in);
            expect(result.tokenType).toBe(newTokens.token_type);

            // Verify tokens are stored
            expect(tokenManager.getAccessToken()).toBe(newTokens.access_token);
            expect(tokenManager.getRefreshToken()).toBe(newTokens.refresh_token);
        });

        it('should throw error when no refresh token is available', async () => {
            await expect(tokenManager.refreshAccessToken()).rejects.toThrow('No refresh token available');
            expect(mockedAxios.post).not.toHaveBeenCalled();
        });

        it('should clear tokens and throw error when refresh fails', async () => {
            tokenManager.setTokens(mockTokens);
            mockedAxios.post.mockRejectedValueOnce(new Error('Network error'));

            await expect(tokenManager.refreshAccessToken()).rejects.toThrow('Failed to refresh token');

            // Verify tokens were cleared
            expect(tokenManager.getAccessToken()).toBeNull();
            expect(tokenManager.getRefreshToken()).toBeNull();
        });

        it('should handle missing token_type in response', async () => {
            const newTokens = {
                access_token: 'new_access_token',
                refresh_token: 'new_refresh_token',
                expires_in: 3600
                // token_type is missing
            };

            tokenManager.setTokens(mockTokens);
            mockedAxios.post.mockResolvedValueOnce({ data: newTokens });

            const result = await tokenManager.refreshAccessToken();

            expect(result.tokenType).toBe('Bearer'); // Should default to 'Bearer'
        });
    });

    describe('getTokenType', () => {
        it('should return stored token type', () => {
            tokenManager.setTokens(mockTokens);
            expect(tokenManager.getTokenType()).toBe('Bearer');
        });

        it('should return default Bearer when no token type is stored', () => {
            expect(tokenManager.getTokenType()).toBe('Bearer');
        });

        it('should return custom token type when stored', () => {
            const customTokens = { ...mockTokens, tokenType: 'token' };
            tokenManager.setTokens(customTokens);
            expect(tokenManager.getTokenType()).toBe('token');
        });
    });
});