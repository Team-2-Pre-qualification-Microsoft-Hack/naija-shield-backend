namespace naija_shield_backend.Services;

/// <summary>
/// Maps Nigerian phone numbers to approximate state-level coordinates.
/// Nigerian mobile numbers carry no embedded location data, so this service
/// uses a deterministic hash of the MSISDN to assign a consistent Nigerian
/// state. Small jitter is added so multiple incidents from the same state
/// do not overlap on the heatmap.
///
/// In production this would be replaced with a real network element lookup
/// (cell tower ID from the telecom switch) or the AT Number Lookup API.
/// </summary>
public sealed class PhoneLocationService
{
    private record StateEntry(string State, string Lga, double Lat, double Lng);

    // Representative cities across Nigeria's 36 states + FCT.
    // Lagos appears twice to reflect its disproportionate fraud volume.
    private static readonly StateEntry[] States =
    [
        new("Lagos",       "Lagos Island",   6.4541,  3.3947),
        new("Lagos",       "Ikeja",          6.6018,  3.3515),
        new("FCT Abuja",   "Municipal",      9.0579,  7.4951),
        new("Kano",        "Fagge",         12.0022,  8.5920),
        new("Rivers",      "Port Harcourt",  4.8156,  7.0498),
        new("Oyo",         "Ibadan",         7.3775,  3.9470),
        new("Anambra",     "Onitsha",        6.1620,  6.7870),
        new("Edo",         "Benin City",     6.3350,  5.6270),
        new("Kaduna",      "Kaduna",        10.5264,  7.4381),
        new("Delta",       "Warri",          5.5182,  5.7499),
        new("Enugu",       "Enugu",          6.4421,  7.4984),
        new("Imo",         "Owerri",         5.4836,  7.0333),
        new("Plateau",     "Jos",            9.8965,  8.8583),
        new("Kwara",       "Ilorin",         8.5373,  4.5444),
        new("Cross River", "Calabar",        4.9517,  8.3220),
        new("Borno",       "Maiduguri",     11.8311, 13.1509),
        new("Sokoto",      "Sokoto",        13.0059,  5.2476),
        new("Osun",        "Osogbo",         7.7670,  4.5624),
        new("Abia",        "Aba",            5.1098,  7.3662),
        new("Bayelsa",     "Yenagoa",        4.9267,  6.2676),
        new("Ogun",        "Abeokuta",       7.1557,  3.3451),
        new("Ondo",        "Akure",          7.2526,  5.1931),
        new("Ekiti",       "Ado Ekiti",      7.6219,  5.2212),
        new("Kogi",        "Lokoja",         7.7956,  6.7358),
        new("Benue",       "Makurdi",        7.7357,  8.5217),
        new("Niger",       "Minna",          9.6139,  6.5568),
        new("Kebbi",       "Birnin Kebbi",  12.4539,  4.1975),
        new("Zamfara",     "Gusau",         12.1704,  6.6636),
        new("Katsina",     "Katsina",       12.9890,  7.5978),
        new("Jigawa",      "Dutse",         11.8627,  9.3449),
        new("Bauchi",      "Bauchi",        10.3158,  9.8442),
        new("Gombe",       "Gombe",         10.2904, 11.1667),
        new("Yobe",        "Damaturu",      11.7469, 11.9696),
        new("Adamawa",     "Yola",           9.2035, 12.4954),
        new("Taraba",      "Jalingo",        8.8959, 11.3597),
        new("Nasarawa",    "Lafia",          8.4940,  8.5214),
        new("Ebonyi",      "Abakaliki",      6.3249,  8.1137),
        new("Akwa Ibom",   "Uyo",            5.0377,  7.9128),
    ];

    public (double Lat, double Lng, string State, string Lga) Lookup(string phoneNumber)
    {
        // Strip non-digits to normalise +234XXXXXXXXXX and 0XXXXXXXXX
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());

        // Deterministic index — same number always maps to the same state
        var hash = Math.Abs(digits.GetHashCode());
        var entry = States[hash % States.Length];

        // Deterministic jitter ±0.4° so nearby incidents don't stack exactly
        var jitterLat = ((hash / States.Length) % 100 - 50) / 125.0;
        var jitterLng = ((hash / (States.Length * 3)) % 100 - 50) / 125.0;

        return (
            Math.Round(entry.Lat + jitterLat, 4),
            Math.Round(entry.Lng + jitterLng, 4),
            entry.State,
            entry.Lga
        );
    }
}
