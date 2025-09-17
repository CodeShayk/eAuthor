import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Container } from 'inversify';
import { ApiClient } from '@/services/ApiClient';
import { TYPES } from '@/types';

describe('ApiClient', () => {
  let container: Container;
  let apiClient: ApiClient;
  let mockHttpClient: any;

  beforeEach(() => {
    container = new Container();
    
    mockHttpClient = {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      setBaseUrl: vi.fn(),
      getBaseUrl: vi.fn().mockReturnValue('https://api.github.com')
    };

    container.bind(TYPES.HttpClient).toConstantValue(mockHttpClient);
    container.bind(TYPES.ApiClient).to(ApiClient);

    apiClient = container.get<ApiClient>(TYPES.ApiClient);
  });

  describe('HTTP methods', () => {
    it('should delegate GET requests to HttpClient', async () => {
      const mockResponse = { data: 'test', status: 200 };
      mockHttpClient.get.mockResolvedValueOnce(mockResponse);

      const result = await apiClient.get('/test');

      expect(mockHttpClient.get).toHaveBeenCalledWith('/test', undefined);
      expect(result).toEqual(mockResponse);
    });

    it('should delegate POST requests to HttpClient', async () => {
      const mockResponse = { data: 'created', status: 201 };
      const testData = { name: 'test' };
      mockHttpClient.post.mockResolvedValueOnce(mockResponse);

      const result = await apiClient.post('/test', testData);

      expect(mockHttpClient.post).toHaveBeenCalledWith('/test', testData, undefined);
      expect(result).toEqual(mockResponse);
    });

    it('should delegate PUT requests to HttpClient', async () => {
      const mockResponse = { data: 'updated', status: 200 };
      const testData = { name: 'updated' };
      mockHttpClient.put.mockResolvedValueOnce(mockResponse);

      const result = await apiClient.put('/test/1', testData);

      expect(mockHttpClient.put).toHaveBeenCalledWith('/test/1', testData, undefined);
      expect(result).toEqual(mockResponse);
    });

    it('should delegate DELETE requests to HttpClient', async () => {
      const mockResponse = { data: null, status: 204 };
      mockHttpClient.delete.mockResolvedValueOnce(mockResponse);

      const result = await apiClient.delete('/test/1');

      expect(mockHttpClient.delete).toHaveBeenCalledWith('/test/1', undefined);
      expect(result).toEqual(mockResponse);
    });
  });

  describe('Base URL management', () => {
    it('should set base URL via HttpClient', () => {
      const newBaseUrl = 'https://api.example.com';
      
      apiClient.setBaseUrl(newBaseUrl);
      
      expect(mockHttpClient.setBaseUrl).toHaveBeenCalledWith(newBaseUrl);
    });

    it('should get base URL via HttpClient', () => {
      const baseUrl = apiClient.getBaseUrl();
      
      expect(mockHttpClient.getBaseUrl).toHaveBeenCalled();
      expect(baseUrl).toBe('https://api.github.com');
    });
  });

  describe('Domain-specific methods', () => {
    it('should get users with pagination', async () => {
      const mockUsers = [{ id: 1, name: 'User 1' }, { id: 2, name: 'User 2' }];
      mockHttpClient.get.mockResolvedValueOnce({ data: mockUsers, status: 200 });

      const result = await apiClient.getUsers(2, 5);

      expect(mockHttpClient.get).toHaveBeenCalledWith('/users', {
        params: { page: 2, limit: 5 }
      });
      expect(result.data).toEqual(mockUsers);
    });

    it('should get user by ID', async () => {
      const mockUser = { id: 1, name: 'User 1' };
      mockHttpClient.get.mockResolvedValueOnce({ data: mockUser, status: 200 });

      const result = await apiClient.getUserById('1');

      expect(mockHttpClient.get).toHaveBeenCalledWith('/users/1');
      expect(result.data).toEqual(mockUser);
    });

    it('should create user', async () => {
      const newUser = { name: 'New User', email: 'new@example.com' };
      const createdUser = { id: 3, ...newUser };
      mockHttpClient.post.mockResolvedValueOnce({ data: createdUser, status: 201 });

      const result = await apiClient.createUser(newUser);

      expect(mockHttpClient.post).toHaveBeenCalledWith('/users', newUser);
      expect(result.data).toEqual(createdUser);
    });

    it('should get repositories', async () => {
      const mockRepos = [
        { id: 1, name: 'repo1', language: 'TypeScript' },
        { id: 2, name: 'repo2', language: 'JavaScript' }
      ];
      mockHttpClient.get.mockResolvedValueOnce({ data: mockRepos, status: 200 });

      const result = await apiClient.getRepositories();

      expect(mockHttpClient.get).toHaveBeenCalledWith('/user/repos');
      expect(result.data).toEqual(mockRepos);
    });

    it('should get specific repository', async () => {
      const mockRepo = { id: 1, name: 'test-repo', owner: 'testuser' };
      mockHttpClient.get.mockResolvedValueOnce({ data: mockRepo, status: 200 });

      const result = await apiClient.getRepository('testuser', 'test-repo');

      expect(mockHttpClient.get).toHaveBeenCalledWith('/repos/testuser/test-repo');
      expect(result.data).toEqual(mockRepo);
    });
  });
});