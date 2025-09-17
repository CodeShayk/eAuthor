import { create } from 'zustand';
import { AuthState, AuthUser, IAuthService, TYPES } from './../types';
import { container } from './../di/container';

interface AuthActions {
    login: (code: string) => Promise<void>;
    logout: () => Promise<void>;
    refreshToken: () => Promise<void>;
    getCurrentUser: () => Promise<void>;
    setLoading: (loading: boolean) => void;
    setError: (error: string | null) => void;
    clearError: () => void;
    checkAuthStatus: () => void;
}

const authService = container.get<IAuthService>(TYPES.AuthService);

export const useAuthStore = create<AuthState & AuthActions>((set, get) => ({
    // State
    isAuthenticated: false,
    user: null,
    tokens: null,
    isLoading: false,
    error: null,

    // Actions
    login: async (code: string) => {
        try {
            set({isLoading: true, error: null});

            const user = await authService.login(code);

            set({
                isAuthenticated: true,
                user,
                isLoading: false,
                error: null
            });
        } catch (error) {
            set({
                isAuthenticated: false,
                user: null,
                isLoading: false,
                error: error instanceof Error ? error.message : 'Login failed'
            });
        }
    },

    logout: async () => {
        try {
            set({isLoading: true});

            await authService.logout();

            set({
                isAuthenticated: false,
                user: null,
                tokens: null,
                isLoading: false,
                error: null
            });
        } catch (error) {
            set({
                isLoading: false,
                error: error instanceof Error ? error.message : 'Logout failed'
            });
        }
    },

    refreshToken: async () => {
        try {
            set({isLoading: true, error: null});

            const tokens = await authService.refreshToken();

            set({
                tokens,
                isLoading: false,
                error: null
            });
        } catch (error) {
            set({
                isLoading: false,
                error: error instanceof Error ? error.message : 'Token refresh failed'
            });
        }
    },

    getCurrentUser: async () => {
        try {
            set({ isLoading: true, error: null });

            const user = await authService.getCurrentUser();

            set({
                isAuthenticated: true,
                user,
                isLoading: false,
                error: null
            });
        } catch (error) {
            set({
                isAuthenticated: false,
                user: null,
                isLoading: false,
                error: error instanceof Error ? error.message : 'Failed to fetch user'
            });
        }
    },

    setLoading: (loading: boolean) => set({ isLoading: loading }),

    setError: (error: string | null) => set({ error }),

    clearError: () => set({ error: null }),

    checkAuthStatus: () => {
        // Implementation depends on your auth logic
        const tokens = get().tokens;
        if (tokens) {
            set({ isAuthenticated: true });
        }
    }
}));