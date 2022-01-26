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
        private readonly ConcurrentDictionary<long, TokenState> _tokens;
        private readonly RandomNumberGenerator _random;
        private static readonly TimeSpan TimeoutPeriod = new TimeSpan(0, 0, 120);
        private const short TokenLength = 4;

        public TokenAuthentication()
        {
            _tokens = new ConcurrentDictionary<long, TokenState>();
            _random = RandomNumberGenerator.Create();
        }

        public bool AuthorizeToken(long networkId, string token)
        {
            var authorizeSuccessful = _tokens.ContainsKey(networkId) && _tokens[networkId].Token == token;

            if (authorizeSuccessful)
            {
                _tokens.TryRemove(networkId, out _);
            }

            return authorizeSuccessful;
        }

        public TokenState GenerateNextToken(long networkId)
        {
            TokenState state;

            if (_tokens.ContainsKey(networkId))
            {
                state = _tokens[networkId];

                if ((DateTime.Now - state.RequestTime) > TimeoutPeriod)
                {
                    _tokens.TryRemove(networkId, out _);
                }

                else
                {
                    return state;
                }
            }

            state = new TokenState
            {
                NetworkId = networkId,
                Token = _generateToken(),
                TokenDuration = TimeoutPeriod
            };

            _tokens.TryAdd(networkId, state);

            // perform some housekeeping so we don't have built up tokens if they're not ever used
            foreach (var (key, value) in _tokens)
            {
                if ((DateTime.Now - value.RequestTime) > TimeoutPeriod)
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
