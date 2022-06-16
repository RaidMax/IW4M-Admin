using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace IW4MAdmin.Application.Misc
{
    internal class TokenAuthentication : ITokenAuthentication
    {
        private readonly ConcurrentDictionary<int, TokenState> _tokens;
        private readonly RandomNumberGenerator _random;
        private static readonly TimeSpan TimeoutPeriod = new(0, 0, 120);
        private const short TokenLength = 4;

        public TokenAuthentication()
        {
            _tokens = new ConcurrentDictionary<int, TokenState>();
            _random = RandomNumberGenerator.Create();
        }

        public bool AuthorizeToken(ITokenIdentifier authInfo)
        {
            var authorizeSuccessful = _tokens.ContainsKey(authInfo.ClientId) &&
                                      _tokens[authInfo.ClientId].Token == authInfo.Token;

            if (authorizeSuccessful)
            {
                _tokens.TryRemove(authInfo.ClientId, out _);
            }

            return authorizeSuccessful;
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

            _tokens.TryAdd(authInfo.ClientId, state);

            // perform some housekeeping so we don't have built up tokens if they're not ever used
            foreach (var (key, value) in _tokens)
            {
                if (DateTime.Now - value.RequestTime > TimeoutPeriod)
                {
                    _tokens.TryRemove(key, out _);
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

            _random.Dispose();
            return token.ToString();
        }
    }
}
