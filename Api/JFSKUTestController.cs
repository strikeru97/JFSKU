using System.Net.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JFSKU.Api;

[ApiController]
[Route("JFSKU")]
[Authorize(Policy = "RequiresElevation")]
public sealed class JFSKUTestController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public JFSKUTestController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public sealed class TestKeyRequest
    {
        public string ApiKey { get; set; } = string.Empty;
    }

    [HttpPost("TestTmdbKey")]
    public async Task<IActionResult> TestTmdbKey(
        [FromBody] TestKeyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { success = false, message = "API-Key ist leer." });
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            using var response = await client.GetAsync(
                "https://api.themoviedb.org/3/configuration?api_key="
                + Uri.EscapeDataString(request.ApiKey.Trim()), timeout.Token);

            return Ok(new
            {
                success = response.IsSuccessStatusCode,
                message = response.IsSuccessStatusCode
                    ? "TMDb API-Key ist gültig. Bitte anschließend speichern."
                    : $"TMDb antwortet mit HTTP {(int)response.StatusCode}."
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(504, new { success = false, message = "Zeitüberschreitung bei TMDb." });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new { success = false, message = "TMDb ist nicht erreichbar." });
        }
    }
}
