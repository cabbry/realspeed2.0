using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RealSpeed2
{
    public class LocationViewModel : INotifyPropertyChanged
    {
        private Location _location;
        public Location Location
        {
            get => _location;
            set
            {
                if (_location?.Timestamp != value.Timestamp)
                {
                    _location = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _realSpeed;
        public string RealSpeed
        {
            get => _realSpeed;
            set
            {
                if (_realSpeed != value)
                {
                    _realSpeed = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _calculatedSpeed;
        public string CalculatedSpeed
        {
            get => _calculatedSpeed;
            set
            {
                if (_calculatedSpeed != value)
                {
                    _calculatedSpeed = value;
                    OnPropertyChanged();
                }
            }
        }

        private GeolocationAccuracy _accuracy;
        public GeolocationAccuracy Accuracy
        {
            get => _accuracy;
            set
            {
                if (_accuracy != value)
                {
                    _accuracy = value;
                    OnPropertyChanged();
                }
            }
        }

        private TimeSpan _timeout;
        public TimeSpan Timeout
        {
            get => _timeout;
            set
            {
                if (_timeout != value)
                {
                    _timeout = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
