// Authentication types
export interface AuthTokens {
    accessToken: string;
    refreshToken: string;
    expiresIn: number;
    tokenType: string;
}

export interface AuthUser {
    id: string;
    email: string;
    name: string;
    avatar?: string;
}

export interface AuthState {
    isAuthenticated: boolean;
    user: AuthUser | null;
    tokens: AuthTokens | null;
    isLoading: boolean;
    error: string | null;
}

// OAuth configuration
export interface OAuthConfig {
    clientId: string;
    redirectUri: string;
    scope: string;
    authUrl: string;
    tokenUrl: string;
    userInfoUrl: string;
}

// API Client types
export interface ApiResponse<T = any> {
    data: T;
    status: number;
    message?: string;
}

export interface ApiError {
    message: string;
    status: number;
    code?: string;
    details?: any;
}

export interface RequestConfig {
    headers?: Record<string, string>;
    params?: Record<string, any>;
    timeout?: number;
}

// Dependency Injection symbols
export const TYPES = {
    ApiClient: Symbol.for('ApiClient'),
    AuthService: Symbol.for('AuthService'),
    TokenManager: Symbol.for('TokenManager'),
    HttpClient: Symbol.for('HttpClient'),
    OAuthConfig: Symbol.for('OAuthConfig')
} as const;

// Service interfaces
export interface ITokenManager {
    getAccessToken(): string | null;
    getRefreshToken(): string | null;
    getTokenType(): string;
    setTokens(tokens: AuthTokens): void;
    clearTokens(): void;
    isTokenExpired(): boolean;
    refreshAccessToken(): Promise<AuthTokens>;
}

export interface IHttpClient {
    get<T>(url: string, config?: RequestConfig): Promise<ApiResponse<T>>;
    post<T>(url: string, data?: any, config?: RequestConfig): Promise<ApiResponse<T>>;
    put<T>(url: string, data?: any, config?: RequestConfig): Promise<ApiResponse<T>>;
    delete<T>(url: string, config?: RequestConfig): Promise<ApiResponse<T>>;
    setBaseUrl(url: string): void;
    getBaseUrl(): string;
}

export interface IApiClient extends IHttpClient {
    setBaseUrl(url: string): void;
    getBaseUrl(): string;
}

export interface IAuthService {
    login(code: string): Promise<AuthUser>;
    logout(): Promise<void>;
    refreshToken(): Promise<AuthTokens>;
    getCurrentUser(): Promise<AuthUser>;
    getAuthUrl(): string;
    isAuthenticated(): boolean;
    validateState(state: string): boolean;
}