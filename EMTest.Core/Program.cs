using System.Globalization;
using CommandLine;
using System.Net;

namespace EMTest
{
    public class Options
    {
        private bool _isParsingOk = true;

        public bool GetParsingStatus()
        {
            return _isParsingOk;
        }

        [Option('l', "file-log", HelpText = "Log file")]
        public string FileLogName { get; set; }

        [Option('o', "file-output", HelpText = "Output file")]
        public string FileOutName { get; set; }

        private IPAddress _addressStart = IPAddress.Parse("0.0.0.0");

        [Option('s', "address-start", HelpText = "Address start")]
        public string AddressStart
        {
            get => _addressStart.ToString();
            set
            {
                if (_isParsingOk)
                {
                    _isParsingOk = IPAddress.TryParse(value, out _addressStart);
                }
            }
        }

        public IPAddress GetAddressStart()
        {
            return _addressStart;
        }

        private IPAddress _addressMask = IPAddress.Parse("255.255.255.255");

        [Option('m', "address-mask", HelpText = "Address mask")]
        public string AddressMask
        {
            get => _addressMask.ToString();

            set
            {
                if (_isParsingOk)
                {
                    _isParsingOk = IPAddress.TryParse(value, out _addressMask);
                }
            }
        }

        public IPAddress GetAddressMask()
        {
            return _addressMask;
        }

        private DateTime _timeStart;

        public DateTime GetTimeStart()
        {
            return _timeStart;
        }

        [Option('s', "time-start", HelpText = "Time start")]
        public string TimeStart
        {
            get => _timeStart.ToString();

            set
            {
                if (_isParsingOk)
                {
                    _isParsingOk = DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _timeStart);
                }
            }
        }

        private DateTime _timeEnd;

        public DateTime GetTimeEnd()
        {
            return _timeEnd;
        }

        [Option('e', "time-end", HelpText = "Time end")]
        public string TimeEnd
        {
            get => _timeEnd.ToString();

            set
            {
                if (_isParsingOk)
                {
                    _isParsingOk = DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _timeEnd);
                    _timeEnd = _timeEnd.Date.AddHours(23).AddMinutes(59)
                        .AddSeconds(59);
                    /* это я делаю из-за того, что задается только дата без времени.
                       Логично, что заказчик хочет получить логи за этот день*/
                }
            }
        }
    }

    public class Program
    {
        static void Main(string[] args)
        {
            Options options = Parser.Default.ParseArguments<Options>(args).Value;
            if (options.GetParsingStatus() == false)
            {
                Console.WriteLine("Wrong data format");
                return;
            }

            LoggedDataManager ldm = new LoggedDataManager(options);
            ldm.FillData();
            ldm.WriteResult();

        }

        static void HandleOptions(Options options)
        {
            Console.WriteLine(
                $"{options.FileOutName} {options.FileLogName} {options.AddressStart} {options.AddressMask} \n\n {options.TimeStart} \n{options.TimeEnd} \n flag = {options.GetParsingStatus()}");
        }
    }

    public class LoggedData
    {
        protected Dictionary<IPAddress, List<DateTime>> _loggedData = new(); //temp
    }

    public class LoggedDataManager : LoggedData
    {
        protected Options _options;

        public LoggedDataManager(Options options)
        {
            _options = options;
        }


        public bool FillData()
        {
            try
            {
                using (FileStream fs = new FileStream(_options.FileLogName, FileMode.Open))
                using (StreamReader reader = new StreamReader(fs))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (TryParseLine(line, out IPAddress ipAddress, out DateTime accessTime))
                        {
                            if (IsIpAddressInRange(ipAddress, _options.GetAddressStart(), _options.GetAddressMask()))
                            {
                                if (accessTime >= _options.GetTimeStart() && accessTime <= _options.GetTimeEnd())
                                {
                                    if (!_loggedData.ContainsKey(ipAddress))
                                    {
                                        _loggedData[ipAddress] = new List<DateTime>();
                                    }

                                    _loggedData[ipAddress].Add(accessTime);
                                }
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while reading log file: {ex.Message}");
                return false;
            }
        }


        public bool WriteResult()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(_options.FileOutName))
                {
                    foreach (var kvp in _loggedData)
                    {
                        IPAddress ipAddress = kvp.Key;
                        List<DateTime> accessTimes = kvp.Value;
                        writer.WriteLine($"IP: {ipAddress}, Count: {accessTimes.Count()}");
//                      }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while writing result: {ex.Message}");
                return false;
            }
        }

        private bool IsIpAddressInRange(IPAddress ipAddress, IPAddress startAddress, IPAddress endAddress)
        {
            byte[] startBytes = startAddress.GetAddressBytes();
            byte[] endBytes = endAddress.GetAddressBytes();
            byte[] ipBytes = ipAddress.GetAddressBytes();

            uint startUInt = ((uint)startBytes[3] << 24) + ((uint)startBytes[2] << 16) + ((uint)startBytes[1] << 8) +
                             ((uint)startBytes[0]);
            uint endUInt = ((uint)endBytes[3] << 24) + ((uint)endBytes[2] << 16) + ((uint)endBytes[1] << 8) +
                           ((uint)endBytes[0]);
            uint ipUInt = ((uint)ipBytes[3] << 24) + ((uint)ipBytes[2] << 16) + ((uint)ipBytes[1] << 8) +
                          ((uint)ipBytes[0]);
            // Понимаю, что не изящно и нарушаю DRY но я удивлен что IPAddress имеет так мало удобных методов

            if (ipUInt >= startUInt && ipUInt <= endUInt)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool TryParseLine(string line, out IPAddress ipAddress, out DateTime accessTime)
        {
            ipAddress = null;
            accessTime = DateTime.MinValue;

            int lastColonIndex = line.IndexOf(':');

            if (lastColonIndex == -1)
            {
                Console.WriteLine($"Invalid log entry: {line}");
                return false;
            }

            string ipAddressPart = line.Substring(0, lastColonIndex);
            string timePart = line.Substring(lastColonIndex + 1);

            if (!IPAddress.TryParse(ipAddressPart, out ipAddress))
            {
                Console.WriteLine($"Invalid IP address: {ipAddressPart}");
                return false;
            }

            if (!DateTime.TryParseExact(timePart, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out accessTime))
            {
                Console.WriteLine($"Invalid access time: {timePart}");
                return false;
            }

            return true;
        }
    }
}