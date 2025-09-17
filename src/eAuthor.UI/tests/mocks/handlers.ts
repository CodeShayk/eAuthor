import { http, HttpResponse } from 'msw';

export const handlers = [
  // OAuth token exchange
  http.post('https://github.com/login/oauth/access_token', () => {
    return HttpResponse.json({
      access_token: 'mock_access_token',
      refresh_token: 'mock_refresh_token',
      expires_in: 3600,
      token_type: 'Bearer'
    });
  }),

  // Get user info
  http.get('https://api.github.com/user', ({ request }) => {
    const authHeader = request.headers.get('Authorization');
    
    if (!authHeader || !authHeader.includes('mock_access_token')) {
      return new HttpResponse(null, { status: 401 });
    }

    return HttpResponse.json({
      id: '123',
      login: 'testuser',
      name: 'Test User',
      email: 'test@example.com',
      avatar_url: 'https://github.com/images/error/octocat_happy.gif'
    });
  }),

  // Get repositories
  http.get('https://api.github.com/user/repos', ({ request }) => {
    const authHeader = request.headers.get('Authorization');
    
    if (!authHeader || !authHeader.includes('mock_access_token')) {
      return new HttpResponse(null, { status: 401 });
    }

    return HttpResponse.json([
      {
        id: 1,
        name: 'test-repo',
        description: 'A test repository',
        language: 'TypeScript',
        stargazers_count: 5,
        html_url: 'https://github.com/testuser/test-repo'
      },
      {
        id: 2,
        name: 'another-repo',
        description: 'Another test repository',
        language: 'JavaScript',
        stargazers_count: 10,
        html_url: 'https://github.com/testuser/another-repo'
      }
    ]);
  }),

  // Refresh token
  http.post('https://github.com/login/oauth/access_token', ({ request }) => {
    return HttpResponse.json({
      access_token: 'new_mock_access_token',
      refresh_token: 'new_mock_refresh_token',
      expires_in: 3600,
      token_type: 'Bearer'
    });
  })
];