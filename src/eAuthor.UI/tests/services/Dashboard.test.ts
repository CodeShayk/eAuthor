import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { BrowserRouter } from 'react-router-dom';
import Dashboard from './../../src/components/Dashboard';
import { useAuthStore } from './../../src/stores/authStore';
import { container } from '././../../src/di/container';
import { TYPES } from './../../src/types';

// Mock the auth store
vi.mock('@/stores/authStore', () => ({
    useAuthStore: vi.fn()
}));

// Mock the DI container
vi.mock('@/di/container', () => ({
    container: {
        get: vi.fn()
    }
}));

describe('Dashboard', () => {
    const mockUser = {
        id: '123',
        name: 'Test User',
        email: 'test@example.com',
        avatar: 'https://github.com/images/error/octocat_happy.gif'
    };

    const mockRepositories = [
        {
            id: 1,
            name: 'test-repo',
            description: 'A test repository',
            language: 'TypeScript',
            stargazers_count: 5
        },
        {
            id: 2,
            name: 'another-repo',
            description: 'Another test repository',
            language: 'JavaScript',
            stargazers_count: 10
        }
    ];

    let mockApiClient: any;
    let mockUseAuthStore: any;

    beforeEach(() => {
        vi.clearAllMocks();

        mockApiClient = {
            getRepositories: vi.fn()
        };

        mockUseAuthStore = vi.fn(() => ({
            user: mockUser,
            logout: vi.fn(),
            isLoading: false
        }));

        (useAuthStore as any).mockImplementation(mockUseAuthStore);
        (container.get as any).mockImplementation((type: symbol) => {
            if (type === TYPES.ApiClient) {
                return mockApiClient;
            }
            return null;
        });
    });

    const renderDashboard = () => {
        return render(
            <BrowserRouter>
                <Dashboard />
            </BrowserRouter>
        );
    };

    it('should render user information', async () => {
        mockApiClient.getRepositories.mockResolvedValueOnce({
            data: mockRepositories
        });

        renderDashboard();

        expect(screen.getByText('Test User')).toBeInTheDocument();
        expect(screen.getByText('test@example.com')).toBeInTheDocument();
        expect(screen.getByText('123')).toBeInTheDocument();
    });

    it('should display loading state', () => {
        mockUseAuthStore.mockReturnValueOnce({
            user: mockUser,
            logout: vi.fn(),
            isLoading: true
        });

        renderDashboard();

        // Look for the loading spinner
        const loadingSpinner = document.querySelector('.animate-spin');
        expect(loadingSpinner).toBeInTheDocument();
    });

    it('should fetch and display repositories', async () => {
        mockApiClient.getRepositories.mockResolvedValueOnce({
            data: mockRepositories
        });

        renderDashboard();

        await waitFor(() => {
            expect(screen.getByText('test-repo')).toBeInTheDocument();
        });

        expect(screen.getByText('A test repository')).toBeInTheDocument();
        expect(screen.getByText('TypeScript')).toBeInTheDocument();
        expect(screen.getByText('⭐ 5')).toBeInTheDocument();

        expect(screen.getByText('another-repo')).toBeInTheDocument();
        expect(screen.getByText('Another test repository')).toBeInTheDocument();
        expect(screen.getByText('JavaScript')).toBeInTheDocument();
        expect(screen.getByText('⭐ 10')).toBeInTheDocument();
    });

    it('should handle API error when fetching repositories', async () => {
        const errorMessage = 'Failed to fetch repositories';
        mockApiClient.getRepositories.mockRejectedValueOnce(new Error(errorMessage));

        renderDashboard();

        await waitFor(() => {
            expect(screen.getByText(errorMessage)).toBeInTheDocument();
        });
    });

    it('should call logout when logout button is clicked', async () => {
        const mockLogout = vi.fn();
        mockUseAuthStore.mockReturnValueOnce({
            user: mockUser,
            logout: mockLogout,
            isLoading: false
        });

        mockApiClient.getRepositories.mockResolvedValueOnce({
            data: mockRepositories
        });

        renderDashboard();

        const logoutButton = screen.getByText('Logout');
        fireEvent.click(logoutButton);

        expect(mockLogout).toHaveBeenCalled();
    });

    it('should refresh repositories when refresh button is clicked', async () => {
        mockApiClient.getRepositories
            .mockResolvedValueOnce({ data: mockRepositories })
            .mockResolvedValueOnce({ data: [...mockRepositories] });

        renderDashboard();

        await waitFor(() => {
            expect(screen.getByText('test-repo')).toBeInTheDocument();
        });

        const refreshButton = screen.getByText('Refresh');
        fireEvent.click(refreshButton);

        expect(mockApiClient.getRepositories).toHaveBeenCalledTimes(2);
    });

    it('should display "No repositories found" when no repositories exist', async () => {
        mockApiClient.getRepositories.mockResolvedValueOnce({
            data: []
        });

        renderDashboard();

        await waitFor(() => {
            expect(screen.getByText('No repositories found')).toBeInTheDocument();
        });
    });

    it('should show loading state for refresh button during API call', async () => {
        let resolvePromise: (value: any) => void;
        const pendingPromise = new Promise((resolve) => {
            resolvePromise = resolve;
        });

        mockApiClient.getRepositories
            .mockResolvedValueOnce({ data: mockRepositories })
            .mockReturnValueOnce(pendingPromise);

        renderDashboard();

        await waitFor(() => {
            expect(screen.getByText('test-repo')).toBeInTheDocument();
        });

        const refreshButton = screen.getByText('Refresh');
        fireEvent.click(refreshButton);

        expect(screen.getByText('Loading...')).toBeInTheDocument();

        // Resolve the pending promise
        resolvePromise!({ data: mockRepositories });

        await waitFor(() => {
            expect(screen.getByText('Refresh')).toBeInTheDocument();
        });
    });

    it('should handle repositories with missing data gracefully', async () => {
        const incompleteRepos = [
            {
                id: 1,
                name: 'incomplete-repo'
                // missing description, language, stargazers_count
            },
            {
                id: 2,
                name: 'another-incomplete',
                language: null,
                stargazers_count: null
            }
        ];

        mockApiClient.getRepositories.mockResolvedValueOnce({
            data: incompleteRepos
        });

        renderDashboard();

        await waitFor(() => {
            expect(screen.getByText('incomplete-repo')).toBeInTheDocument();
        });

        expect(screen.getByText('No description')).toBeInTheDocument();
        expect(screen.getByText('Unknown')).toBeInTheDocument();
        expect(screen.getByText('⭐ 0')).toBeInTheDocument();
    });

    it('should display user avatar when available', () => {
        mockApiClient.getRepositories.mockResolvedValueOnce({
            data: mockRepositories
        });

        renderDashboard();

        const avatar = screen.getByAltText('Test User');
        expect(avatar).toBeInTheDocument();
        expect(avatar).toHaveAttribute('src', mockUser.avatar);
    });

    it('should handle missing user data gracefully', () => {
        const incompleteUser = {
            id: '123',
            name: null,
            email: null,
            avatar: null
        };

        mockUseAuthStore.mockReturnValueOnce({
            user: incompleteUser,
            logout: vi.fn(),
            isLoading: false
        });

        mockApiClient.getRepositories.mockResolvedValueOnce({
            data: []
        });

        renderDashboard();

        expect(screen.getByText('N/A')).toBeInTheDocument(); // For missing name and email
    });

    it('should limit repositories display to 10 items', async () => {
        const manyRepos = Array.from({ length: 15 }, (_, i) => ({
            id: i + 1,
            name: `repo-${i + 1}`,
            description: `Repository ${i + 1}`,
            language: 'JavaScript',
            stargazers_count: i
        }));

        mockApiClient.getRepositories.mockResolvedValueOnce({
            data: manyRepos
        });

        renderDashboard();

        await waitFor(() => {
            expect(screen.getByText('repo-1')).toBeInTheDocument();
        });

        // Should display first 10 repos
        expect(screen.getByText('repo-10')).toBeInTheDocument();

        // Should not display repos beyond 10
        expect(screen.queryByText('repo-11')).not.toBeInTheDocument();
        expect(screen.queryByText('repo-15')).not.toBeInTheDocument();
    });
});