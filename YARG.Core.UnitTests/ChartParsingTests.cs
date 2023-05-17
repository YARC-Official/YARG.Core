using MoonscraperChartEditor.Song;
using NUnit.Framework;
using MoonscraperChartEditor.Song.IO;

namespace YARG.Core.UnitTests {
    public class ChartParsingTests
    {
        private string? projectDirectory;
        private string? fullChartPath;
        private MoonSong song;
        
        [SetUp]
        public void Setup()
        {
            // This will get the current WORKING directory (i.e. \bin\Debug)
            string workingDirectory = Environment.CurrentDirectory;
            
            // This will get the current PROJECT directory
            projectDirectory = Directory.GetParent(workingDirectory)?.Parent?.Parent?.FullName;
        }
        
        [TestCase("test.chart")]
        public void ParseChartFile(string notesFile)
        {
            Assert.DoesNotThrow(() =>
            {
                if (projectDirectory == null)
                {
                    throw new NullReferenceException();
                }
                
                fullChartPath = Path.Combine(projectDirectory, "Test Charts", notesFile);
                song = ChartReader.ReadChart(fullChartPath);
            });
        }
        
        [TestCase("test.mid")]
        public void ParseMidiFile(string notesFile)
        { 
            MidReader.CallbackState state = default;
            
            Assert.DoesNotThrow(() =>
            {
                if (projectDirectory == null)
                {
                    throw new NullReferenceException();
                }
                
                fullChartPath = Path.Combine(projectDirectory, "Test Charts", notesFile);
                song = MidReader.ReadMidi(fullChartPath, ref state);
                Assert.That(state, Is.EqualTo(MidReader.CallbackState.None));
            });
        }
    }
}