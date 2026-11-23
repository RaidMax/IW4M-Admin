using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using IW4MAdmin.Application.API.Master;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Responses;
using Microsoft.Extensions.Logging;
using Refit;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Misc;

public class GeoLocationService : IGeoLocationService
{
    private readonly Lazy<DatabaseReader> _reader;
    private readonly IMasterApi _masterApi;
    private readonly ApplicationConfiguration _appConfig;
    private readonly ILogger _logger;

    public GeoLocationService(string sourceAddress, IMasterApi masterApi, ApplicationConfiguration appConfig,
        ILogger<GeoLocationService> logger)
    {
        // DatabaseReader is thread-safe, so open the database once instead of per lookup
        _reader = new Lazy<DatabaseReader>(() => new DatabaseReader(sourceAddress));
        _masterApi = masterApi;
        _appConfig = appConfig;
        _logger = logger;
    }

    public async Task<GeoLocationDetails> LocateDetailed(string address, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        try
        {
            var language = SharedLibraryCore.Utilities.CurrentLocalization?.LocalizationName ?? "en-US";
            var result = await _masterApi.GetGeoLocation(address, language, _appConfig.Id, token);

            // the master returns success = false for private/reserved addresses or when no source has data
            return result is { Success: true } ? result : null;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Could not retrieve detailed geolocation for {Address}", address);
            throw;
        }
    }

    public Task<IGeoLocationResult> Locate(string address)
    {
        // country names are returned in the application's configured language when MaxMind has a translation
        var languageTag = SharedLibraryCore.Utilities.CurrentLocalization?.LocalizationName;
        CountryResponse country = null;

        try
        {
            _reader.Value.TryCountry(address, out country);
        }
        catch
        {
            // ignored
        }

        var response = new GeoLocationResult
        {
            Country = LocalizedName(country?.Country.Names, languageTag) ?? country?.Country.Name ?? "Unknown",
            CountryCode = country?.Country.IsoCode ?? ""
        };

        return Task.FromResult((IGeoLocationResult)response);
    }

    private static string LocalizedName(IReadOnlyDictionary<string, string> names, string languageTag)
    {
        if (names is null || names.Count == 0 || string.IsNullOrEmpty(languageTag))
        {
            return null;
        }

        // MaxMind locales are "en", "de", "pt-BR", "zh-CN" etc.
        if (names.TryGetValue(languageTag, out var name))
        {
            return name;
        }

        var language = languageTag.Split('-')[0];
        return names.FirstOrDefault(kvp => kvp.Key.Split('-')[0]
            .Equals(language, StringComparison.OrdinalIgnoreCase)).Value;
    }
}
