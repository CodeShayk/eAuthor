import { injectable, inject } from 'inversify';
import { IApiClient, IHttpClient, ApiResponse, RequestConfig, TYPES } from './../types';

@injectable()
export class ApiClient implements IApiClient {
  constructor(
    @inject(TYPES.HttpClient) private httpClient: IHttpClient
  ) {}

  async get<T>(url: string, config?: RequestConfig): Promise<ApiResponse<T>> {
    return this.httpClient.get<T>(url, config);
  }

  async post<T>(url: string, data?: any, config?: RequestConfig): Promise<ApiResponse<T>> {
    return this.httpClient.post<T>(url, data, config);
  }

  async put<T>(url: string, data?: any, config?: RequestConfig): Promise<ApiResponse<T>> {
    return this.httpClient.put<T>(url, data, config);
  }

  async delete<T>(url: string, config?: RequestConfig): Promise<ApiResponse<T>> {
    return this.httpClient.delete<T>(url, config);
  }

  setBaseUrl(url: string): void {
    this.httpClient.setBaseUrl(url);
  }

  getBaseUrl(): string {
    return this.httpClient.getBaseUrl();
  }

  // Domain-specific API methods
  async getUsers(page = 1, limit = 10): Promise<ApiResponse<any[]>> {
    return this.get('/users', { params: { page, limit } });
  }

  async getUserById(id: string): Promise<ApiResponse<any>> {
    return this.get(`/users/${id}`);
  }

  async createUser(userData: any): Promise<ApiResponse<any>> {
    return this.post('/users', userData);
  }

  async updateUser(id: string, userData: any): Promise<ApiResponse<any>> {
    return this.put(`/users/${id}`, userData);
  }

  async deleteUser(id: string): Promise<ApiResponse<any>> {
    return this.delete(`/users/${id}`);
  }

  // GitHub specific endpoints (example)
  async getRepositories(): Promise<ApiResponse<any[]>> {
    return this.get('/user/repos');
  }

  async getRepository(owner: string, repo: string): Promise<ApiResponse<any>> {
    return this.get(`/repos/${owner}/${repo}`);
  }

  async createRepository(repoData: any): Promise<ApiResponse<any>> {
    return this.post('/user/repos', repoData);
  }
}