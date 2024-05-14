using MaxMind.Db;
using MaxMind.GeoIP2;

namespace Torrential
{
    public sealed class GeoIpService
    {
        private readonly DatabaseReader _countryDbReader = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeoLite2-Country.mmdb"), FileAccessMode.Memory);

        public ValueTask<string> GetCountryCodeAsync(string ip)
        {
            var country = _countryDbReader.Country(ip);
            if (country.Country == null)
                return new ValueTask<string>("");

            return new ValueTask<string>(country.Country.IsoCode ?? "");
        }
    }
}
