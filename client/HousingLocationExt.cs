using XivCommon.Functions.Housing;

namespace OrangeGuidanceTomestone;

internal static class HousingLocationExt {
    internal static ushort? CombinedPlot(this HousingLocation housing) {
        if (housing is { Apartment: { } apt, ApartmentWing: { } wing }) {
            return (ushort) (10_000
                             + (wing - 1) * 5_000
                             + apt);
        }

        if (housing.Plot is { } plotNum) {
            return plotNum;
        }

        return null;
    }
}
