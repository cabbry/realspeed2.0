using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RealSpeed2
{
    public static class CalculGPS
    {
        private const double EarthRadiusKm = 6371.0; // Rayon de la Terre en km

        public static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            double lat1Rad = DegreesToRadians(lat1);
            double lon1Rad = DegreesToRadians(lon1);
            double lat2Rad = DegreesToRadians(lat2);
            double lon2Rad = DegreesToRadians(lon2);

            double dLat = lat2Rad - lat1Rad;
            double dLon = lon2Rad - lon1Rad;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadiusKm * c;
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        public static double CalculateSpeedKmh(double lat1, double lon1, DateTime time1, double lat2, double lon2, DateTime time2)
        {
            // Calculer la distance en kilomètres entre les deux points GPS
            double distanceKm = HaversineDistance(lat1, lon1, lat2, lon2);

            // Calculer le temps écoulé en heures
            double timeHours = (time2 - time1).TotalHours;

            // Calculer la vitesse en km/h
            return distanceKm / timeHours;
        }
    }
}
