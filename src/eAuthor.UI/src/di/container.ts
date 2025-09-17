import 'reflect-metadata';
import { Container } from 'inversify';
import {
    TYPES,
    IApiClient,
    IAuthService,
    ITokenManager,
    IHttpClient,
    OAuthConfig
} from './../types';
import { ApiClient } from './../services/ApiClient';
import { AuthService } from './../services/AuthService';
import { TokenManager } from './../services/TokenManager';
import { HttpClient } from './../services/HttpClient';
import { oauthConfig } from './../config/oauth';

const container = new Container();

// Bind configuration first
container.bind<OAuthConfig>(TYPES.OAuthConfig).toConstantValue(oauthConfig);

// Bind TokenManager first (no dependencies on other services)
container.bind<ITokenManager>(TYPES.TokenManager).to(TokenManager).inSingletonScope();

// Bind HttpClient (depends on TokenManager)
container.bind<IHttpClient>(TYPES.HttpClient).to(HttpClient).inSingletonScope();

// Bind higher-level services
container.bind<IApiClient>(TYPES.ApiClient).to(ApiClient).inSingletonScope();
container.bind<IAuthService>(TYPES.AuthService).to(AuthService).inSingletonScope();

export { container };