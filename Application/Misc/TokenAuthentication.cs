using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace IW4MAdmin.Application.Misc
{
    internal class TokenAuthentication : ITokenAuthentication
    {
        private readonly ConcurrentDictionary<int, TokenState> _tokens = new();
        private readonly RandomNumberGenerator _random = RandomNumberGenerator.Create();
        private static readonly TimeSpan TimeoutPeriod = new(0, 0, 120);
        private const short TokenLength = 4;
        private const int MaxFailedAttempts = 5;
        private readonly ConcurrentDictionary<int, int> _failedAttempts = new();

        public bool AuthorizeToken(ITokenIdentifier authInfo)
        {
            if (authInfo?.Token is null || !_tokens.TryGetValue(authInfo.ClientId, out var state))
            {
                return false;
            }

            // a token is only good for the lifetime it was advertised with
            if (DateTime.Now - state.RequestTime > state.TokenDuration)
            {
                Invalidate(authInfo.ClientId, state);
                return false;
            }

            if (CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state.Token ?? string.Empty),
                    Encoding.UTF8.GetBytes(authInfo.Token)))
            {
                // single use
                Invalidate(authInfo.ClientId, state);
                return true;
            }

            // a short token must not survive guessing. password logins also reach here first, so allow a few
            // misses (a mistyped password or token) before the outstanding token is thrown away
            var failures = _failedAttempts.AddOrUpdate(authInfo.ClientId, 1, (_, count) => count + 1);
            if (failures >= MaxFailedAttempts)
            {
                Invalidate(authInfo.ClientId, state);
            }

            return false;
        }

        private void Invalidate(int clientId, TokenState state)
        {
            _tokens.TryRemove(new KeyValuePair<int, TokenState>(clientId, state));
            _failedAttempts.TryRemove(clientId, out _);
        }

        public TokenState GenerateNextToken(ITokenIdentifier authInfo)
        {
            TokenState state;

            if (_tokens.ContainsKey(authInfo.ClientId))
            {
                state = _tokens[authInfo.ClientId];

                if (DateTime.Now - state.RequestTime > TimeoutPeriod)
                {
                    _tokens.TryRemove(authInfo.ClientId, out _);
                }

                else
                {
                    return state;
                }
            }

            state = new TokenState
            {
                Token = _generateToken(),
                TokenDuration = TimeoutPeriod
            };

            _tokens[authInfo.ClientId] = state;
            _failedAttempts.TryRemove(authInfo.ClientId, out _);

            // perform some housekeeping so we don't have built up tokens if they're not ever used
            foreach (var (key, value) in _tokens)
            {
                if (DateTime.Now - value.RequestTime > TimeoutPeriod)
                {
                    _tokens.TryRemove(key, out _);
                    _failedAttempts.TryRemove(key, out _);
                }
            }

            return state;
        }

        private string _generateToken()
        {
            bool ValidCharacter(char c)
            {
                // this ensure that the characters are 0-9, A-Z, a-z
                return (c > 47 && c < 58) || (c > 64 && c < 91) || (c > 96 && c < 123);
            }

            var token = new StringBuilder();

            var charSet = new byte[1];
            while (token.Length < TokenLength)
            {
                _random.GetBytes(charSet);

                if (ValidCharacter((char)charSet[0]))
                {
                    token.Append((char)charSet[0]);
                }
            }

            return token.ToString();
        }
    }
}
