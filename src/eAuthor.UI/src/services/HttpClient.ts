import { injectable, inject } from 'inversify';
import axios, { AxiosInstance, AxiosRequestConfig, AxiosResponse } from 'axios';
import { IHttpClient, ITokenManager, ApiResponse, ApiError, RequestConfig, TYPES } from './../types';
import { apiConfig } from './../config/oauth';

@injectable()
export class HttpClient implements IHttpClient {
    private axiosInstance: AxiosInstance;

    constructor(
        @inject(TYPES.TokenManager) private tokenManager: ITokenManager
    ) {
        this.axiosInstance = axios.create({
            baseURL: apiConfig.baseUrl,
            timeout: apiConfig.timeout,
            headers: {
                'Content-Type': 'application/json',
                'Accept': 'application/json'
            }
        });

        this.setupInterceptors();
    }

    private setupInterceptors(): void {
        // Request interceptor - add auth token
        this.axiosInstance.interceptors.request.use(
            async (config) => {
                const token = this.tokenManager.getAccessToken();

                if (token && !this.tokenManager.isTokenExpired()) {
                    config.headers.Authorization = `${this.tokenManager.getTokenType()} ${token}`;
                }

                return config;
            },
            (error) => Promise.reject(error)
        );

        // Response interceptor - handle errors and token refresh
        this.axiosInstance.interceptors.response.use(
            (response) => response,
            async (error) => {
                const originalRequest = error.config;

                if (error.response?.status === 401 && !originalRequest._retry) {
                    originalRequest._retry = true;

                    try {
                        // Let TokenManager handle its own refresh logic
                        await this.tokenManager.refreshAccessToken();
                        const newToken = this.tokenManager.getAccessToken();

                        if (newToken) {
                            originalRequest.headers.Authorization = `${this.tokenManager.getTokenType()} ${newToken}`;
                            return this.axiosInstance(originalRequest);
                        }
                    } catch (refreshError) {
                        // Refresh failed, clear tokens and redirect
                        this.tokenManager.clearTokens();
                        if (typeof window !== 'undefined') {
                            window.location.href = '/login';
                        }
                        return Promise.reject(refreshError);
                    }
                }

                return Promise.reject(this.handleApiError(error));
            }
        );
    }

    private handleApiError(error: any): ApiError {
        if (error.response) {
            return {
                message: error.response.data?.message || error.message || 'An error occurred',
                status: error.response.status,
                code: error.response.data?.code,
                details: error.response.data
            };
        } else if (error.request) {
            return {
                message: 'Network error - no response received',
                status: 0,
                code: 'NETWORK_ERROR'
            };
        } else {
            return {
                message: error.message || 'An unexpected error occurred',
                status: 0,
                code: 'UNKNOWN_ERROR'
            };
        }
    }

    private transformResponse<T>(response: AxiosResponse): ApiResponse<T> {
        return {
            data: response.data,
            status: response.status,
            message: response.statusText
        };
    }

    async get<T>(url: string, config?: RequestConfig): Promise<ApiResponse<T>> {
        const axiosConfig: AxiosRequestConfig = {
            ...config,
            params: config?.params
        };

        const response = await this.axiosInstance.get<T>(url, axiosConfig);
        return this.transformResponse<T>(response);
    }

    async post<T>(url: string, data?: any, config?: RequestConfig): Promise<ApiResponse<T>> {
        const response = await this.axiosInstance.post<T>(url, data, config);
        return this.transformResponse<T>(response);
    }

    async put<T>(url: string, data?: any, config?: RequestConfig): Promise<ApiResponse<T>> {
        const response = await this.axiosInstance.put<T>(url, data, config);
        return this.transformResponse<T>(response);
    }

    async delete<T>(url: string, config?: RequestConfig): Promise<ApiResponse<T>> {
        const response = await this.axiosInstance.delete<T>(url, config);
        return this.transformResponse<T>(response);
    }

    setBaseUrl(url: string): void {
        this.axiosInstance.defaults.baseURL = url;
    }

    getBaseUrl(): string {
        return this.axiosInstance.defaults.baseURL || '';
    }
}