# React Project Template

A professional React TypeScript project with OAuth authentication, custom API client, dependency injection, and comprehensive testing.

## Features

- 🔐 **OAuth 2.0 Authentication** - Complete OAuth flow with GitHub integration
- 🔄 **Automatic Token Refresh** - Seamless token renewal with retry logic
- 🏗️ **Dependency Injection** - Inversify.js for clean architecture
- 📡 **Custom API Client** - Axios-based HTTP client with interceptors
- 🧪 **Comprehensive Testing** - Unit tests with Vitest and Testing Library
- 📱 **Responsive UI** - Tailwind CSS for modern design
- 🔧 **Modern Tooling** - Vite, TypeScript, ESLint
- 📦 **Production Ready** - Optimized build with code splitting

## Project Structure

```
src/
├── components/          # React components
│   ├── Dashboard.tsx    # Main dashboard component
│   ├── Login.tsx        # Login page component
│   ├── AuthCallback.tsx # OAuth callback handler
│   └── PrivateRoute.tsx # Protected route wrapper
├── services/            # Business logic services
│   ├── AuthService.ts   # Authentication service
│   ├── TokenManager.ts  # Token management
│   ├── HttpClient.ts    # HTTP client with interceptors
│   └── ApiClient.ts     # Domain-specific API methods
├── stores/              # State management
│   └── authStore.ts     # Zustand auth store
├── di/                  # Dependency injection
│   └── container.ts     # IoC container configuration
├── types/               # TypeScript definitions
│   └── index.ts         # Shared interfaces and types
├── config/              # Configuration
│   └── oauth.ts         # OAuth and API configuration
└── App.tsx              # Main application component

tests/
├── components/          # Component tests
├── services/            # Service tests
├── mocks/              # Mock handlers for MSW
└── setup.ts            # Test configuration
```

## Getting Started

### Prerequisites

- Node.js 18+ 
- npm or yarn
- OAuth app credentials (GitHub, Google, etc.)

### Installation

1. Clone the repository:
```bash
git clone <repository-url>
cd react-oauth-api-client
```

2. Install dependencies:
```bash
npm install
```

3. Configure environment variables:
```bash
cp .env.example .env
```

Edit `.env` with your OAuth credentials:
```env
VITE_OAUTH_CLIENT_ID=your_github_client_id
VITE_OAUTH_REDIRECT_URI=http://localhost:5173/auth/callback
VITE_OAUTH_SCOPE=read:user user:email
VITE_OAUTH_AUTH_URL=https://github.com/login/oauth/authorize
VITE_OAUTH_TOKEN_URL=https://github.com/login/oauth/access_token
VITE_OAUTH_USER_INFO_URL=https://api.github.com/user
VITE_API_BASE_URL=https://api.github.com
```

### Development

Start the development server:
```bash
npm run dev
```

The application will be available at `http://localhost:5173`

### Building

Build for production:
```bash
npm run build
```

Preview the production build:
```bash
npm run preview
```

### Testing

Run tests:
```bash
npm test
```

Run tests with UI:
```bash
npm run test:ui
```

Generate coverage report:
```bash
npm run test:coverage
```

### Linting

Check code quality:
```bash
npm run lint
```

Type checking:
```bash
npm run type-check
```

## Architecture

### Dependency Injection

The project uses Inversify.js for dependency injection, providing loose coupling and easy testing:

```typescript
// Services are bound in the container
container.bind<IApiClient>(TYPES.ApiClient).to(ApiClient).inSingletonScope();
container.bind<IAuthService>(TYPES.AuthService).to(AuthService).inSingletonScope();

// And injected into components/services
@injectable()
export class AuthService implements IAuthService {
  constructor(
    @inject(TYPES.TokenManager) private tokenManager: ITokenManager,
    @inject(TYPES.HttpClient) private httpClient: IHttpClient
  ) {}
}
```

### Token Management

Automatic token refresh with retry logic:
- Tokens are stored in localStorage
- Automatic refresh before expiration
- Request interceptors handle token attachment
- Failed refresh redirects to login

### API Client

The API client provides:
- Automatic authentication headers
- Request/response interceptors
- Error handling
- Type-safe methods
- Domain-specific endpoints

### State Management

Using Zustand for simple, effective state management:
- Authentication state
- Loading states
- Error handling
- Actions for login/logout

## OAuth Setup

### GitHub OAuth App

1. Go to GitHub Settings > Developer settings > OAuth Apps
2. Create a new OAuth App with:
   - Application name: Your app name
   - Homepage URL: `http://localhost:5173`
   - Authorization callback URL: `http://localhost:5173/auth/callback`
3. Copy the Client ID and Client Secret to your `.env` file

### Other Providers

The OAuth implementation is generic and can work with other providers by updating the configuration in `src/config/oauth.ts`.

## Deployment

The application builds to a `dist/` folder and can be deployed to any static hosting service:

- **Netlify**: Deploy the `dist` folder
- **Vercel**: Connect your repository
- **AWS S3**: Upload `dist` contents to S3 bucket
- **GitHub Pages**: Use GitHub Actions to deploy

Remember to update the OAuth redirect URI for production!

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/new-feature`
3. Make your changes
4. Add tests for new functionality
5. Run tests and linting: `npm test && npm run lint`
6. Commit your changes: `git commit -am 'Add new feature'`
7. Push to the branch: `git push origin feature/new-feature`
8. Submit a pull request

## License

MIT License - see LICENSE file for details.

## Support

For questions or issues, please create an issue in the repository or contact the maintainers.
