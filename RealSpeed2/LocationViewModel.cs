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
        private Location? _location;
        public Location? Location
        {
            get => _location;
            set
            {
                _location = value;
                OnPropertyChanged();
            }
        }

        private int _updateCount;
        public int UpdateCount
        {
            get => _updateCount;
            set
            {
                if (_updateCount != value)
                {
                    _updateCount = value;
                    OnPropertyChanged();
                }
            }
        }

        private DateTime _lastUpdate;
        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set
            {
                _lastUpdate = value;
                OnPropertyChanged();
            }
        }

        private string _realSpeed = string.Empty;
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

        private string _calculatedSpeed = string.Empty;
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

        private string _maxSpeed = "0.0";
        public string MaxSpeed
        {
            get => _maxSpeed;
            set
            {
                if (_maxSpeed != value)
                {
                    _maxSpeed = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
