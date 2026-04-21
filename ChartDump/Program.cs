using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoonscraperChartEditor.Song;
using YARG.Core;
using YARG.Core.Chart;

/// <summary>
/// Parses a chart file with YARG.Core's MoonSong parser (Moonscraper-based)
/// and dumps the raw parsed data as JSON. This is the intermediate representation
/// BEFORE game-specific transformations, preserving all parsed data.
/// </summary>
class Program
{
    static readonly MoonSong.Difficulty[] AllDiffs = {
        MoonSong.Difficulty.Easy, MoonSong.Difficulty.Medium,
        MoonSong.Difficulty.Hard, MoonSong.Difficulty.Expert
    };

    static int Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "--batch")
        {
            var limit = args.Length >= 4 ? int.Parse(args[3]) : 0;
            return DumpAll.Run(args[1], args[2], limit);
        }

        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: ChartDump <path-to-chart-folder-or-file>");
            Console.Error.WriteLine("       ChartDump --batch <input-dir> <output-dir> [limit]");
            return 1;
        }

        var path = args[0];
        string chartFile;

        if (Directory.Exists(path))
        {
            chartFile = Path.Combine(path, "notes.chart");
            if (!File.Exists(chartFile))
                chartFile = Path.Combine(path, "notes.mid");
            if (!File.Exists(chartFile))
            {
                Console.Error.WriteLine($"No notes.chart or notes.mid found in {path}");
                return 1;
            }
        }
        else if (File.Exists(path))
        {
            chartFile = path;
        }
        else
        {
            Console.Error.WriteLine($"Path not found: {path}");
            return 1;
        }

        try
        {
            var dump = ParseAndDump(chartFile);
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };
            Console.WriteLine(JsonSerializer.Serialize(dump, options));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Parse error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 2;
        }
    }

    public static Dictionary<string, object?> ParseAndDump(string chartFile)
    {
        var ext = Path.GetExtension(chartFile).ToLowerInvariant();
        var settings = ext == ".mid" ? ParseSettings.Default_Midi : ParseSettings.Default_Chart;

        // Read song.ini next to the chart file so we pick up drum-type hints
        // (five_lane_drums / pro_drums). Without these, SongChart auto-populates
        // all three drum interpretations and we can't tell which one is canonical.
        // Also read hopo_frequency / eighthnote_hopo / hopofreq so the parser
        // uses the same HOPO threshold the game will — matching YARG.Core's
        // SongEntry.IniBase ini handling.
        string chartDir = Path.GetDirectoryName(chartFile) ?? "";
        string iniPath = Path.Combine(chartDir, "song.ini");
        DrumsType iniDrumsType = DrumsType.Unknown;
        long? iniHopoFrequency = null;
        bool? iniEighthnoteHopo = null;
        int? iniHopofreq = null;
        if (File.Exists(iniPath))
        {
            try
            {
                foreach (var line in File.ReadAllLines(iniPath))
                {
                    var trimmed = line.Trim();
                    bool ReadBool(string key, out bool value)
                    {
                        value = false;
                        if (!trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase)) return false;
                        var eq = trimmed.IndexOf('=');
                        if (eq < 0) return false;
                        var raw = trimmed.Substring(eq + 1).Trim();
                        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1") { value = true; return true; }
                        if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw == "0") { value = false; return true; }
                        return false;
                    }
                    bool ReadInt(string key, out long value)
                    {
                        value = 0;
                        if (!trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase)) return false;
                        var eq = trimmed.IndexOf('=');
                        if (eq < 0) return false;
                        return long.TryParse(trimmed.Substring(eq + 1).Trim(), out value);
                    }
                    if (ReadBool("five_lane_drums", out var isFiveLane) && isFiveLane)
                        iniDrumsType = DrumsType.FiveLane;
                    else if (ReadBool("pro_drums", out var isProDrums) && isProDrums)
                    {
                        if (iniDrumsType != DrumsType.FiveLane) iniDrumsType = DrumsType.ProDrums;
                    }
                    else if (ReadInt("hopo_frequency", out var hopoFreq))
                        iniHopoFrequency = hopoFreq;
                    else if (ReadBool("eighthnote_hopo", out var eighthHopo))
                        iniEighthnoteHopo = eighthHopo;
                    else if (ReadInt("hopofreq", out var hf))
                        iniHopofreq = (int)hf;
                }
            }
            catch { /* ignore ini read errors */ }
        }
        // Seed settings so the parser disambiguates to the right drum type and
        // SongChart only populates the canonical drum track.
        settings.DrumsType = iniDrumsType;

        // Apply HOPO threshold from ini — this matches SongEntry.IniBase's logic.
        // Resolution-dependent computation happens in MidReader/ChartReader based
        // on settings.HopoThreshold (or the default fallback there).
        // We read TicksPerQuarterNote from the MIDI to compute absolute ticks.
        if (iniHopoFrequency.HasValue && iniHopoFrequency.Value > 0)
        {
            settings.HopoThreshold = iniHopoFrequency.Value;
        }
        else if (iniEighthnoteHopo.HasValue)
        {
            // Need resolution. Quick-parse the MIDI header to get ticksPerBeat
            // without invoking a full parser. For .chart we fall through and
            // let ChartReader apply its own default (resolution/3 + 1).
            if (ext == ".mid")
            {
                try
                {
                    using var fs = File.OpenRead(chartFile);
                    using var reader = new BinaryReader(fs);
                    // MThd chunk: magic(4) + length(4) + format(2) + tracks(2) + division(2)
                    reader.ReadBytes(8);
                    reader.ReadUInt16();
                    reader.ReadUInt16();
                    var division = (ushort)((reader.ReadByte() << 8) | reader.ReadByte());
                    int resolution = division & 0x7FFF;
                    settings.HopoThreshold = resolution / (iniEighthnoteHopo.Value ? 2 : 3);
                }
                catch { /* ignore */ }
            }
        }
        else if (iniHopofreq.HasValue)
        {
            // Not common — leave default for now.
        }

        MoonSong song;
        if (ext == ".mid")
            song = MoonscraperChartEditor.Song.IO.MidReader.ReadMidi(ref settings, chartFile);
        else
            song = MoonscraperChartEditor.Song.IO.ChartReader.ReadFromFile(ref settings, chartFile);

        // Build SongChart for normalized vocals
        SongChart? songChart = null;
        try
        {
            songChart = SongChart.FromFile(settings, chartFile);
        }
        catch { /* vocals parsing may fail on some charts */ }

        return BuildDump(song, ext, songChart, iniDrumsType);
    }

    static Dictionary<string, object?> BuildDump(MoonSong song, string ext, SongChart? songChart, DrumsType iniDrumsType)
    {
        var dump = new Dictionary<string, object?>
        {
            ["resolution"] = song.resolution,
            ["format"] = ext == ".mid" ? "mid" : "chart",
        };

        // Sync track
        dump["tempos"] = song.syncTrack.Tempos.Select(t => new
        {
            tick = t.Tick,
            beatsPerMinute = Math.Round(t.BeatsPerMinute, 6),
        }).ToArray();

        dump["timeSignatures"] = song.syncTrack.TimeSignatures.Select(ts => new
        {
            tick = ts.Tick,
            numerator = ts.Numerator,
            denominator = ts.Denominator,
        }).ToArray();

        // Global events
        dump["sections"] = song.sections.Select(s => new
        {
            tick = s.tick,
            text = s.text,
        }).ToArray();

        dump["globalEvents"] = song.events.Select(e => new
        {
            tick = e.tick,
            text = e.text,
        }).ToArray();

        // Venue
        if (song.venue.Count > 0)
        {
            dump["venue"] = song.venue.Select(v => new
            {
                tick = v.tick,
                length = v.length,
                type = v.type.ToString(),
                text = v.text,
            }).ToArray();
        }

        // Note: Beatlines are NOT dumped because MoonSong auto-generates them
        // via FinishLoading() when the BEAT track is absent. We can't distinguish
        // parsed-from-file beatlines from auto-generated ones after parsing.
        // The BEAT track data is preserved separately in unknownMidiTracks.

        // Charts (per instrument × difficulty)
        var tracks = new List<object>();

        foreach (var instrument in Enum.GetValues<MoonSong.MoonInstrument>())
        {
            foreach (var diff in AllDiffs)
            {
                var chart = song.GetChart(instrument, diff);
                if (chart.IsEmpty) continue;

                tracks.Add(new
                {
                    instrument = instrument.ToString(),
                    difficulty = diff.ToString().ToLower(),
                    gameMode = chart.gameMode.ToString(),

                    notes = chart.notes.Select(n => new
                    {
                        tick = n.tick,
                        rawNote = n.rawNote,
                        length = n.length,
                        flags = (int)n.flags,
                        flagNames = GetFlagNames(n.flags),
                    }).ToArray(),

                    phrases = chart.specialPhrases.Select(p => new
                    {
                        tick = p.tick,
                        length = p.length,
                        type = p.type.ToString(),
                    }).ToArray(),

                    textEvents = chart.events.Count > 0
                        ? chart.events.Select(e => new
                        {
                            tick = e.tick,
                            text = e.text,
                        }).ToArray()
                        : null,

                    animations = chart.animations.Count > 0
                        ? chart.animations.Select(a => new
                        {
                            tick = a.tick,
                            length = a.length,
                            type = a.type.ToString(),
                            text = a.text,
                        }).ToArray()
                        : null,
                });
            }
        }

        dump["tracks"] = tracks;

        // Normalized vocals from SongChart
        if (songChart != null)
        {
            var vocalsTracks = new Dictionary<string, object?>();
            foreach (var (name, voxTrack) in new[] {
                ("vocals", songChart.Vocals),
                ("harmony", songChart.Harmony),
            })
            {
                if (voxTrack.IsEmpty) continue;
                vocalsTracks[name] = DumpVocalsTrack(voxTrack);
            }
            if (vocalsTracks.Count > 0)
                dump["vocalsTracks"] = vocalsTracks;
        }

        // Normalized instrument tracks from SongChart. This is the POST-resolve
        // representation: force flags have been collapsed into concrete note Types
        // (Strum/Hopo/Tap for guitar, Neutral/Accent/Ghost for drums, etc.), so
        // two charts that play identically will match bit-for-bit here even if
        // one has redundant "author marked" force flags in its raw MIDI.
        if (songChart != null)
        {
            var normalizedTracks = new List<object>();

            // Five-fret + six-fret guitar-like instruments
            foreach (var track in songChart.FiveFretTracks.Concat(songChart.SixFretTracks))
            {
                if (track.IsEmpty) continue;
                foreach (var diff in AllYargDiffs)
                {
                    if (!track.TryGetDifficulty(diff, out var diffChart) || diffChart.Notes.Count == 0) continue;
                    normalizedTracks.Add(new
                    {
                        instrument = track.Instrument.ToString(),
                        difficulty = diff.ToString().ToLower(),
                        notes = diffChart.Notes.Select(n => DumpGuitarNote(n)).ToArray(),
                    });
                }
            }

            // Drums: emit only the canonical drum track based on ini hint.
            // YARG's SongChart populates all three (FourLane / ProDrums /
            // FiveLane) from the same raw source, and the "right" one depends
            // on song.ini. Without this filter the comparison sees meaningless
            // pad-index diffs between non-canonical interpretations.
            YARG.Core.Chart.InstrumentTrack<YARG.Core.Chart.DrumNote>? canonicalDrumTrack = null;
            if (iniDrumsType == DrumsType.FiveLane)
                canonicalDrumTrack = songChart.FiveLaneDrums;
            else if (iniDrumsType == DrumsType.ProDrums)
                canonicalDrumTrack = songChart.ProDrums;
            else if (iniDrumsType == DrumsType.FourLane)
                canonicalDrumTrack = songChart.FourLaneDrums;
            else
            {
                // No ini hint — prefer FourLane (scan-chart's default), falling
                // back to ProDrums / FiveLane if FourLane is empty.
                if (!songChart.FourLaneDrums.IsEmpty) canonicalDrumTrack = songChart.FourLaneDrums;
                else if (!songChart.ProDrums.IsEmpty) canonicalDrumTrack = songChart.ProDrums;
                else if (!songChart.FiveLaneDrums.IsEmpty) canonicalDrumTrack = songChart.FiveLaneDrums;
            }
            if (canonicalDrumTrack != null && !canonicalDrumTrack.IsEmpty)
            {
                foreach (var diff in AllYargDiffs)
                {
                    if (!canonicalDrumTrack.TryGetDifficulty(diff, out var diffChart) || diffChart.Notes.Count == 0) continue;
                    normalizedTracks.Add(new
                    {
                        instrument = canonicalDrumTrack.Instrument.ToString(),
                        difficulty = diff.ToString().ToLower(),
                        notes = diffChart.Notes.Select(n => DumpDrumNote(n)).ToArray(),
                    });
                }
            }

            if (normalizedTracks.Count > 0)
                dump["normalizedTracks"] = normalizedTracks;
        }

        return dump;
    }

    static readonly YARG.Core.Difficulty[] AllYargDiffs = {
        YARG.Core.Difficulty.Easy,
        YARG.Core.Difficulty.Medium,
        YARG.Core.Difficulty.Hard,
        YARG.Core.Difficulty.Expert,
    };

    static object DumpGuitarNote(YARG.Core.Chart.GuitarNote n)
    {
        // Represent chord as a flat list of siblings — emit each note in the
        // parent-chord group, keyed by fret + resolved type.
        var fretsInChord = new List<int> { n.Fret };
        foreach (var child in n.ChildNotes) fretsInChord.Add(child.Fret);
        fretsInChord.Sort();
        return new
        {
            tick = n.Tick,
            tickLength = n.TickLength,
            frets = fretsInChord.ToArray(),
            type = n.Type.ToString(),
            isStarPower = n.IsStarPower,
            isSolo = n.IsSolo,
        };
    }

    static object DumpDrumNote(YARG.Core.Chart.DrumNote n)
    {
        // Represent chord as pad list sorted by pad number + per-pad type/dynamics.
        // Sorting is critical so that same-tick chord reorderings don't show up
        // as diffs during comparison — scan-chart's writer doesn't preserve the
        // original per-note order within a chord.
        var padRecords = new List<(int pad, string type)> { (n.Pad, n.Type.ToString()) };
        foreach (var child in n.ChildNotes) padRecords.Add((child.Pad, child.Type.ToString()));
        padRecords.Sort((a, b) => a.pad.CompareTo(b.pad));
        var pads = padRecords.Select(p => new { pad = p.pad, type = p.type }).ToArray();
        return new
        {
            tick = n.Tick,
            tickLength = n.TickLength,
            pads,
            isStarPower = n.IsStarPower,
            isSolo = n.IsSolo,
            isStarPowerActivator = (n.DrumFlags & YARG.Core.Chart.DrumNoteFlags.StarPowerActivator) != 0,
        };
    }

    static object DumpVocalsTrack(VocalsTrack voxTrack)
    {
        return new
        {
            rangeShifts = voxTrack.RangeShifts.Select(rs => new
            {
                tick = rs.Tick,
                tickLength = rs.TickLength,
                minimumPitch = rs.MinimumPitch,
                maximumPitch = rs.MaximumPitch,
            }).ToArray(),

            parts = voxTrack.Parts.Select((part, index) => new
            {
                partIndex = index,
                isHarmony = part.IsHarmony,

                notePhrases = part.NotePhrases.Select(phrase => new
                {
                    tick = phrase.Tick,
                    tickLength = phrase.TickLength,
                    isStarPower = phrase.IsStarPower,
                    isPercussion = phrase.IsPercussion,

                    notes = phrase.PhraseParentNote.ChildNotes.Select(n => new
                    {
                        tick = n.Tick,
                        tickLength = n.TickLength,
                        pitch = n.Pitch,
                        type = n.Type.ToString(),
                    }).ToArray(),

                    lyrics = phrase.Lyrics.Select(l => new
                    {
                        tick = l.Tick,
                        text = l.Text,
                        flags = (int)l.Flags,
                        flagNames = l.Flags != 0 ? l.Flags.ToString() : null,
                    }).ToArray(),
                }).ToArray(),

                staticLyricPhrases = part.StaticLyricPhrases.Count > 0
                    ? part.StaticLyricPhrases.Select(phrase => new
                    {
                        tick = phrase.Tick,
                        tickLength = phrase.TickLength,
                        lyrics = phrase.Lyrics.Select(l => new
                        {
                            tick = l.Tick,
                            text = l.Text,
                            flags = (int)l.Flags,
                        }).ToArray(),
                    }).ToArray()
                    : null,

                otherPhrases = part.OtherPhrases.Count > 0
                    ? part.OtherPhrases.Select(p => new
                    {
                        tick = p.Tick,
                        tickLength = p.TickLength,
                        type = p.Type.ToString(),
                    }).ToArray()
                    : null,
            }).ToArray(),
        };
    }

    static string[]? GetFlagNames(MoonNote.Flags flags)
    {
        if (flags == MoonNote.Flags.None) return null;
        var names = new List<string>();
        foreach (MoonNote.Flags f in Enum.GetValues<MoonNote.Flags>())
        {
            if (f != MoonNote.Flags.None && flags.HasFlag(f))
                names.Add(f.ToString());
        }
        return names.Count > 0 ? names.ToArray() : null;
    }
}
