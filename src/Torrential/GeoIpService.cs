using MaxMind.Db;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Torrential
{
    public sealed class GeoIpService(ILogger<GeoIpService> logger)
    {
        private readonly DatabaseReader _countryDbReader = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeoLite2-Country.mmdb"), FileAccessMode.Memory);

        public ValueTask<string> GetCountryCodeAsync(IPAddress ip)
        {
            try
            {
                var country = _countryDbReader.Country(ip);
                if (country.Country == null)
                    return new ValueTask<string>("");

                return new ValueTask<string>(country.Country.IsoCode ?? "");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error getting country code for IP {Ip}", ip);
                return new ValueTask<string>("");
            }
        }
    }
}
