using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SharedLibraryCore;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebfrontCore.Controllers.API.Dtos;

namespace WebfrontCore.Controllers.API
{
    /// <summary>
    /// api controller for client operations
    /// </summary>
    [ApiController]
    [Route("api/client")]
    public class ClientController : ControllerBase
    {
        private readonly IResourceQueryHelper<FindClientRequest, FindClientResult> _clientQueryHelper;
        private readonly ILogger _logger;

        public ClientController(ILogger logger, IResourceQueryHelper<FindClientRequest, FindClientResult> clientQueryHelper)
        {
            _logger = logger;
            _clientQueryHelper = clientQueryHelper;
        }

        [HttpGet("find")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> FindAsync([FromQuery]FindClientRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ErrorResponse()
                {
                    Messages = ModelState.Values.SelectMany(_value => _value.Errors.Select(_error => _error.ErrorMessage)).ToArray()
                });
            }

            try
            {
                var results = await _clientQueryHelper.QueryResource(request);

                return Ok(new FindClientResponse
                {
                    TotalFoundClients = results.TotalResultCount,
                    Clients = results.Results
                });
            }

            catch (Exception e)
            {
                _logger.WriteWarning($"Failed to retrieve clients with query - {request.ToDebugString()}");
                _logger.WriteDebug(e.GetExceptionInfo());

                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse()
                {
                    Messages = new[] { e.Message }
                });
            }
        }
    }
}
