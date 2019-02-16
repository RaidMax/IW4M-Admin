using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace IW4MAdmin.Application.Misc
{
    class TokenAuthentication : ITokenAuthentication
    {
        private readonly ConcurrentDictionary<long, TokenState> _tokens;
        private readonly RNGCryptoServiceProvider _random;
        private readonly static TimeSpan _timeoutPeriod = new TimeSpan(0, 0, 120);
        private const short TOKEN_LENGTH = 4;

        public TokenAuthentication()
        {
            _tokens = new ConcurrentDictionary<long, TokenState>();
            _random = new RNGCryptoServiceProvider();
        }

        public bool AuthorizeToken(long networkId, string token)
        {
            bool authorizeSuccessful = _tokens.ContainsKey(networkId) && _tokens[networkId].Token == token;

            if (authorizeSuccessful)
            {
                _tokens.TryRemove(networkId, out TokenState _);
            }

            return authorizeSuccessful;
        }

        public TokenState GenerateNextToken(long networkId)
        {
            TokenState state = null;

            if (_tokens.ContainsKey(networkId))
            {
                state = _tokens[networkId];

                if ((DateTime.Now - state.RequestTime) > _timeoutPeriod)
                {
                    _tokens.TryRemove(networkId, out TokenState _);
                }

                else
                {
                    return state;
                }
            }

            state = new TokenState()
            {
                NetworkId = networkId,
                Token = _generateToken(),
                TokenDuration = _timeoutPeriod
            };

            _tokens.TryAdd(networkId, state);

            // perform some housekeeping so we don't have built up tokens if they're not ever used
            foreach (var (key, value) in _tokens)
            {
                if ((DateTime.Now - value.RequestTime) > _timeoutPeriod)
                {
                    _tokens.TryRemove(key, out TokenState _);
                }
            }

            return state;
        }

        public string _generateToken()
        {
            bool validCharacter(char c)
            {
                // this ensure that the characters are 0-9, A-Z, a-z
                return (c > 47 && c < 58) || (c > 64 && c < 91) || (c > 96 && c < 123);
            }

            StringBuilder token = new StringBuilder();

            while (token.Length < TOKEN_LENGTH)
            {
                byte[] charSet = new byte[1];
                _random.GetBytes(charSet);

                if (validCharacter((char)charSet[0]))
                {
                    token.Append((char)charSet[0]);
                }
            }

            _random.Dispose();
            return token.ToString();
        }
    }
}
