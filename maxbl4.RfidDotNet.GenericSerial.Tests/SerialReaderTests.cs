using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using maxbl4.RfidDotNet.GenericSerial.Model;
using maxbl4.RfidDotNet.GenericSerial.Packets;
using maxbl4.RfidDotNet.Infrastructure;
using RJCP.IO.Ports;
using Shouldly;
using Xunit;

namespace maxbl4.RfidDotNet.GenericSerial.Tests
{
    [Collection("Hardware")]
    [Trait("Hardware", "True")]
    public class SerialReaderTests
    {
        [Fact]
        public void Should_get_serial_number()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                for (int i = 0; i < 5; i++)
                {
                    r.GetSerialNumber().Result.ShouldBeOneOf((uint)0x17439015, (uint)406196256);
                }
            }
        }
        
        [Fact]
        public void Should_get_reader_info()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                //r.SetAntennaCheck(false).Wait();
                r.SetRFPower(20).Wait();
                var info = r.GetReaderInfo().Result;
                info.FirmwareVersion.Major.ShouldBe(3);
                info.FirmwareVersion.Minor.ShouldBeInRange(1,18);
                info.Model.ShouldBeOneOf(ReaderModel.CF_RU5202, ReaderModel.UHFReader288MP);
                info.SupportedProtocols.ShouldBeOneOf(ProtocolType.Gen18000_6C, 
                    ProtocolType.Gen18000_6B, ProtocolType.Gen18000_6C|ProtocolType.Gen18000_6B);
                info.RFPower.ShouldBe((byte)20);
                info.InventoryScanInterval.ShouldBeInRange(TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(25500));
                info.AntennaConfiguration.ShouldBe(AntennaConfiguration.Antenna1);
                info.AntennaCheck.ShouldBe(false);
            }
        }
        
        [Fact]
        public void Should_set_inventory_scan_interval()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                r.SetInventoryScanInterval(TimeSpan.FromMilliseconds(1000)).Wait();
                r.GetReaderInfo().Result.InventoryScanInterval.ShouldBe(TimeSpan.FromMilliseconds(1000));
                r.SetInventoryScanInterval(TimeSpan.FromMilliseconds(300)).Wait();
                r.GetReaderInfo().Result.InventoryScanInterval.ShouldBe(TimeSpan.FromMilliseconds(300));
            }
        }
        
        [Fact]
        public void Should_set_rf_power()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                r.SetRFPower(20).Wait();
                r.GetReaderInfo().Result.RFPower.ShouldBe((byte)20);
                r.SetRFPower(0).Wait();
                r.GetReaderInfo().Result.RFPower.ShouldBe((byte)0);
                r.SetRFPower(26).Wait();
                r.GetReaderInfo().Result.RFPower.ShouldBe((byte)26);
            }
        }
        
        [Fact]
        public void Should_get_and_set_epc_length()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                r.SetEpcLengthForBufferOperations(EpcLength.UpTo496Bits).Wait();
                r.GetEpcLengthForBufferOperations().Result.ShouldBe(EpcLength.UpTo496Bits);
                r.SetEpcLengthForBufferOperations(EpcLength.UpTo128Bits).Wait();
                r.GetEpcLengthForBufferOperations().Result.ShouldBe(EpcLength.UpTo128Bits);
            }
        }
        
        [Fact]
        public void Should_get_number_of_tags_in_buffer()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                r.GetNumberOfTagsInBuffer().Wait();
            }
        }
        
        [Fact]
        public void Should_run_tag_inventory_with_default_params()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                r.TagInventory().Wait();
            }
        }
        
        [Fact]
        public void Should_run_tag_inventory_with_optional_params()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                r.TagInventory(new TagInventoryParams
                {
                    QValue = 10,
                    Session = SessionValue.S1,
                    OptionalParams = new TagInventoryOptionalParams(TimeSpan.FromMilliseconds(1000))
                }).Wait();
            }
        }
        
        [Fact]
        public void Should_run_inventory_with_buffer()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                r.TagInventoryWithMemoryBuffer().Wait();
            }
        }
        
        [Fact]
        public void Should_clear_buffer()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                r.ClearBuffer().Wait();
            }
        }
        
        [Fact]
        public void Should_get_tags_from_buffer_empty()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                r.ClearBuffer().Wait();
                var buffer = r.GetTagsFromBuffer().Result;
                buffer.Tags.Count.ShouldBe(0);
            }
        }
        
        [Fact]
        public void Should_read_known_tags()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                r.SetRFPower(26).Wait();
                r.SetInventoryScanInterval(TimeSpan.FromSeconds(10)).Wait();
                var tags = new List<Tag>();
                Timing.StartWait(() =>
                {
                    tags.AddRange(r.TagInventory().Result.Tags);
                    return tags.Select(x => x.TagId).Distinct().Any();
                }).Result.ShouldBeTrue();
                tags.Select(x => x.TagId)
                    .Intersect(TestSettings.Instance.GetKnownTagIds)
                    .Count()
                    .ShouldBeGreaterThanOrEqualTo(1,
                        $"Should find at least one tag from known tags list. " +
                        $"Actually found: {string.Join(", ", tags.Select(x => x.TagId))}");
                tags[0].Rssi.ShouldBeGreaterThan(0);
                tags[0].ReadCount.ShouldBe(1);
                tags[0].LastSeenTime.ShouldBeGreaterThan(DateTimeOffset.Now.Date);
                tags[0].DiscoveryTime.ShouldBeGreaterThan(DateTimeOffset.Now.Date);
            }
        }
        
        [Fact]
        public void Should_run_inventory_with_buffer_and_get_response()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                var totalTagsBuffered = 0;
                var lastInventoryAgg = 0;
                Timing.StartWait(() =>
                {
                    var res = r.TagInventoryWithMemoryBuffer().Result;
                    totalTagsBuffered = res.TagsInBuffer;
                    lastInventoryAgg += res.TagsInLastInventory;
                    return lastInventoryAgg > 50;
                }).Result.ShouldBeTrue("Failed to read 50 tags");
                lastInventoryAgg.ShouldBeGreaterThan(50);
                totalTagsBuffered.ShouldBeInRange(1, 100);
                
                r.GetNumberOfTagsInBuffer().Result.ShouldBe(totalTagsBuffered);
                var tagInBuffer = r.GetTagsFromBuffer().Result;
                tagInBuffer.Tags.Count.ShouldBe(totalTagsBuffered);
                tagInBuffer.Tags.Select(x => x.TagId)
                    .Intersect(TestSettings.Instance.GetKnownTagIds)
                    .Count()
                    .ShouldBeGreaterThanOrEqualTo(1,
                        $"Should find at least one tag from known tags list. " +
                        $"Actually found: {string.Join(", ", tagInBuffer.Tags.Select(x => x.TagId))}");
                tagInBuffer.Tags[0].Antenna.ShouldBe(0);
                tagInBuffer.Tags[0].Rssi.ShouldBeGreaterThan(0);
                tagInBuffer.Tags[0].LastSeenTime.ShouldBeGreaterThan(DateTimeOffset.Now.Date);
                tagInBuffer.Tags[0].DiscoveryTime.ShouldBeGreaterThan(DateTimeOffset.Now.Date);
            }
        }
        
        //[Fact]
        [Trait("MultiAntenna", "true")]
        public void Should_set_antenna_configuration()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                r.SetAntennaConfiguration(AntennaConfiguration.Antenna1).Wait();
                r.GetReaderInfo().Result.AntennaConfiguration.ShouldBe(AntennaConfiguration.Antenna1);
                r.SetAntennaConfiguration(AntennaConfiguration.Antenna1|AntennaConfiguration.Antenna2).Wait();
                r.GetReaderInfo().Result.AntennaConfiguration.ShouldBe(AntennaConfiguration.Antenna1|AntennaConfiguration.Antenna2);
            }
        }
        
        //[Fact]
        [Trait("MultiAntenna", "true")]
        public void Should_set_antenna_check()
        {
            using (var r = new SerialReader(TestSettings.Instance.GetConnection()))
            {
                r.SetAntennaCheck(true).Wait();
                r.GetReaderInfo().Result.AntennaCheck.ShouldBeTrue();
                r.SetAntennaCheck(false).Wait();
                r.GetReaderInfo().Result.AntennaCheck.ShouldBeFalse();
            }
        }
    }
}