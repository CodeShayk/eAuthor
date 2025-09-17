import { injectable, inject } from 'inversify';
import axios from 'axios';
import { IAuthService, ITokenManager, IHttpClient, AuthUser, AuthTokens, OAuthConfig, TYPES } from './../types';

@injectable()
export class AuthService implements IAuthService {
    constructor(
        @inject(TYPES.TokenManager) private tokenManager: ITokenManager,
        @inject(TYPES.HttpClient) private httpClient: IHttpClient,
        @inject(TYPES.OAuthConfig) private oauthConfig: OAuthConfig
    ) {}

    getAuthUrl(): string {
        const params = new URLSearchParams({
            client_id: this.oauthConfig.clientId,
            redirect_uri: this.oauthConfig.redirectUri,
            scope: this.oauthConfig.scope,
            response_type: 'code',
            state: this.generateState()
        });

        return `${this.oauthConfig.authUrl}?${params.toString()}`;
    }

    async login(code: string): Promise<AuthUser> {
        try {
            // Exchange code for tokens using direct axios to avoid circular dependency
            const tokens = await this.exchangeCodeForTokens(code);

            // Store tokens
            this.tokenManager.setTokens(tokens);

            // Get user info using the configured HttpClient
            const user = await this.getCurrentUser();

            return user;
        } catch (error) {
            throw new Error(`Login failed: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    async logout(): Promise<void> {
        this.tokenManager.clearTokens();
        // Optional: Make request to revoke tokens on server
    }

    async refreshToken(): Promise<AuthTokens> {
        return this.tokenManager.refreshAccessToken();
    }

    async getCurrentUser(): Promise<AuthUser> {
        try {
            const response = await this.httpClient.get<any>(this.oauthConfig.userInfoUrl);

            // Transform GitHub user response to our AuthUser format
            const githubUser = response.data;
            return {
                id: githubUser.id?.toString() || githubUser.login,
                email: githubUser.email || '',
                name: githubUser.name || githubUser.login,
                avatar: githubUser.avatar_url
            };
        } catch (error) {
            throw new Error(`Failed to get user info: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    private async exchangeCodeForTokens(code: string): Promise<AuthTokens> {
        try {
            // Use direct axios instance to avoid circular dependency during initial token exchange
            const response = await axios.post(this.oauthConfig.tokenUrl, {
                client_id: this.oauthConfig.clientId,
                code,
                redirect_uri: this.oauthConfig.redirectUri,
                grant_type: 'authorization_code'
            }, {
                headers: {
                    'Accept': 'application/json',
                    'Content-Type': 'application/json'
                }
            });

            return {
                accessToken: response.data.access_token,
                refreshToken: response.data.refresh_token,
                expiresIn: response.data.expires_in,
                tokenType: response.data.token_type || 'Bearer'
            };
        } catch (error) {
            throw new Error(`Token exchange failed: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
    }

    private generateState(): string {
        const state = Math.random().toString(36).substring(2, 15) +
            Math.random().toString(36).substring(2, 15);

        // Only use sessionStorage in browser environment
        if (typeof sessionStorage !== 'undefined') {
            sessionStorage.setItem('oauth_state', state);
        }

        return state;
    }

    validateState(state: string): boolean {
        if (typeof sessionStorage === 'undefined') {
            return true; // Skip validation in non-browser environments
        }

        const storedState = sessionStorage.getItem('oauth_state');
        sessionStorage.removeItem('oauth_state');
        return storedState === state;
    }

    isAuthenticated(): boolean {
        const token = this.tokenManager.getAccessToken();
        return token !== null && !this.tokenManager.isTokenExpired();
    }
}