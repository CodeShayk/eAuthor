import { OAuthConfig } from '@/types';

export const oauthConfig: OAuthConfig = {
  clientId: import.meta.env.VITE_OAUTH_CLIENT_ID || 'your-client-id',
  redirectUri: import.meta.env.VITE_OAUTH_REDIRECT_URI || 'http://localhost:5173/auth/callback',
  scope: import.meta.env.VITE_OAUTH_SCOPE || 'read:user user:email',
  authUrl: import.meta.env.VITE_OAUTH_AUTH_URL || 'https://github.com/login/oauth/authorize',
  tokenUrl: import.meta.env.VITE_OAUTH_TOKEN_URL || 'https://github.com/login/oauth/access_token',
  userInfoUrl: import.meta.env.VITE_OAUTH_USER_INFO_URL || 'https://api.github.com/user'
};

export const apiConfig = {
  baseUrl: import.meta.env.VITE_API_BASE_URL || 'https://api.github.com',
  timeout: parseInt(import.meta.env.VITE_API_TIMEOUT || '10000', 10)
};