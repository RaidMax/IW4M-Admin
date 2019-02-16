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
        private readonly static TimeSpan _timeoutPeriod = new TimeSpan(0, 0, 30);
        private const short TOKEN_LENGTH = 4;

        private class TokenState
        {
            public long NetworkId { get; set; }
            public DateTime RequestTime { get; set; } = DateTime.Now;
            public string Token { get; set; }
        }

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

        public string GenerateNextToken(long networkId)
        {
            TokenState state = null;
            if (_tokens.ContainsKey(networkId))
            {
                state = _tokens[networkId];

                if ((DateTime.Now - state.RequestTime) < _timeoutPeriod)
                {
                    return null;
                }

                else
                {
                    _tokens.TryRemove(networkId, out TokenState _);
                }
            }

            state = new TokenState()
            {
                NetworkId = networkId,
                Token = _generateToken()
            };

            _tokens.TryAdd(networkId, state);
            return state.Token;
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
