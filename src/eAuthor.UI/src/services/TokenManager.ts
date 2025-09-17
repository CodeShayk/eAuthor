import { injectable } from 'inversify';
import axios from 'axios';
import { AuthTokens, ITokenManager } from './../types';
import { oauthConfig } from './../config/oauth';

@injectable()
export class TokenManager implements ITokenManager {
    private readonly STORAGE_PREFIX = 'oauth_';

    getAccessToken(): string | null {
        return localStorage.getItem(`${this.STORAGE_PREFIX}access_token`);
    }

    getRefreshToken(): string | null {
        return localStorage.getItem(`${this.STORAGE_PREFIX}refresh_token`);
    }

    setTokens(tokens: AuthTokens): void {
        const expiresAt = Date.now() + (tokens.expiresIn * 1000);

        localStorage.setItem(`${this.STORAGE_PREFIX}access_token`, tokens.accessToken);
        localStorage.setItem(`${this.STORAGE_PREFIX}refresh_token`, tokens.refreshToken);
        localStorage.setItem(`${this.STORAGE_PREFIX}expires_at`, expiresAt.toString());
        localStorage.setItem(`${this.STORAGE_PREFIX}token_type`, tokens.tokenType);
    }

    clearTokens(): void {
        localStorage.removeItem(`${this.STORAGE_PREFIX}access_token`);
        localStorage.removeItem(`${this.STORAGE_PREFIX}refresh_token`);
        localStorage.removeItem(`${this.STORAGE_PREFIX}expires_at`);
        localStorage.removeItem(`${this.STORAGE_PREFIX}token_type`);
    }

    isTokenExpired(): boolean {
        const expiresAt = localStorage.getItem(`${this.STORAGE_PREFIX}expires_at`);
        if (!expiresAt) return true;

        const expiration = parseInt(expiresAt, 10);
        const now = Date.now();

        // Consider token expired 5 minutes before actual expiration
        return now >= (expiration - 300000);
    }

    async refreshAccessToken(): Promise<AuthTokens> {
        const refreshToken = this.getRefreshToken();

        if (!refreshToken) {
            throw new Error('No refresh token available');
        }

        try {
            // Use a separate axios instance to avoid circular dependency
            const response = await axios.post(oauthConfig.tokenUrl, {
                grant_type: 'refresh_token',
                refresh_token: refreshToken,
                client_id: oauthConfig.clientId
            }, {
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                }
            });

            const tokens: AuthTokens = {
                accessToken: response.data.access_token,
                refreshToken: response.data.refresh_token,
                expiresIn: response.data.expires_in,
                tokenType: response.data.token_type || 'Bearer'
            };

            this.setTokens(tokens);
            return tokens;
        } catch (error) {
            this.clearTokens();
            throw new Error('Failed to refresh token');
        }
    }

    getTokenType(): string {
        return localStorage.getItem(`${this.STORAGE_PREFIX}token_type`) || 'Bearer';
    }
}