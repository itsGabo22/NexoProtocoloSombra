using System.Collections.Generic;
using System.IO;
using Nexo.Cinematics;
using NUnit.Framework;

namespace Nexo.Tests
{
    /// <summary>
    /// Lógica pura de las cinemáticas: rutas, ids de transición, regla de salto, subtítulos y lectura de cabecera MP4.
    /// </summary>
    public sealed class CinematicsLogicTests
    {
        private const string Root = "C:/Game/Nexo_Data/StreamingAssets";

        // ---------- Rutas ----------

        [Test]
        public void VideoPath_IsStreamingAssetsCinematicsIdMp4()
        {
            Assert.AreEqual(Path.Combine(Root, "Cinematics", "intro.mp4"), CinematicPaths.VideoPath(Root, "intro"));
            Assert.AreEqual(Path.Combine(Root, "Cinematics", "ending.captions.json"), CinematicPaths.CaptionsPath(Root, "ending"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("../intro")]
        [TestCase("intro.mp4")]
        [TestCase("sub/intro")]
        [TestCase("C:\\intro")]
        public void VideoPath_RejectsUnsafeIds(string id)
        {
            Assert.IsFalse(CinematicPaths.IsValidId(id));
            Assert.AreEqual(string.Empty, CinematicPaths.VideoPath(Root, id));
            Assert.AreEqual(string.Empty, CinematicPaths.CaptionsPath(Root, id));
        }

        [Test]
        public void VideoPath_EmptyRootGivesEmptyPath()
        {
            Assert.AreEqual(string.Empty, CinematicPaths.VideoPath(null, "intro"));
            Assert.AreEqual(string.Empty, CinematicPaths.VideoPath(string.Empty, "intro"));
        }

        [Test]
        public void AllExpectedIds_AreValid()
        {
            foreach (string id in CinematicIds.Required) Assert.IsTrue(CinematicPaths.IsValidId(id), id);
            foreach (string id in CinematicIds.Optional) Assert.IsTrue(CinematicPaths.IsValidId(id), id);
            CollectionAssert.AreEqual(new[] { "intro", "floor_transition", "ending" }, CinematicIds.Required);
        }

        // ---------- Transición entre pisos ----------

        [TestCase(6, "floor_transition_to_06")]
        [TestCase(1, "floor_transition_to_01")]
        [TestCase(12, "floor_transition_to_12")]
        [TestCase(0, "")]
        [TestCase(-1, "")]
        public void ForFloor_UsesTwoDigits(int floor, string expected)
        {
            Assert.AreEqual(expected, CinematicIds.ForFloor(floor));
        }

        [Test]
        public void ResolveFloorTransition_PrefersSpecificClipWhenItExists()
        {
            var existing = new HashSet<string> { "floor_transition_to_06", "floor_transition" };
            Assert.AreEqual("floor_transition_to_06", CinematicIds.ResolveFloorTransition(6, existing.Contains));
            Assert.AreEqual("floor_transition", CinematicIds.ResolveFloorTransition(1, existing.Contains));
        }

        [Test]
        public void ResolveFloorTransition_FallsBackToGeneric()
        {
            Assert.AreEqual("floor_transition", CinematicIds.ResolveFloorTransition(6, null));
            Assert.AreEqual("floor_transition", CinematicIds.ResolveFloorTransition(-1, id => true));
            Assert.AreEqual("floor_transition", CinematicIds.ResolveFloorTransition(6, id => false));
        }

        // ---------- Regla de salto ----------

        [TestCase(0f, false)]
        [TestCase(0.2f, false)]
        [TestCase(0.49f, false)]
        [TestCase(0.5f, true)]
        [TestCase(3f, true)]
        public void Skip_OnlyAfterHalfSecond(float elapsed, bool expected)
        {
            Assert.AreEqual(expected, CinematicSkipRule.CanSkip(elapsed));
        }

        [TestCase(0.5f, false)]
        [TestCase(0.99f, false)]
        [TestCase(1f, true)]
        public void Hint_AppearsAfterOneSecond(float elapsed, bool expected)
        {
            Assert.AreEqual(expected, CinematicSkipRule.ShowHint(elapsed));
        }

        [Test]
        public void Prepare_TimesOutAtFiveSeconds()
        {
            Assert.IsFalse(CinematicSkipRule.PrepareTimedOut(4.9f));
            Assert.IsTrue(CinematicSkipRule.PrepareTimedOut(5f));
        }

        // ---------- Subtítulos ----------

        [Test]
        public void Captions_ParseSortAndSelect()
        {
            const string json = "[{\"start\":3.0,\"end\":5.0,\"speaker\":\"ELENA\",\"text\":\"Corre.\"}," +
                                "{\"start\":0.5,\"end\":2.5,\"speaker\":\"ELENA\",\"text\":\"Alex, ¿me oyes?\"}]";
            CinematicCaptionTrack track = CinematicCaptionTrack.Parse(json);

            Assert.AreEqual(2, track.Count);
            Assert.AreEqual("Alex, ¿me oyes?", track.Lines[0].text);
            Assert.AreEqual(-1, track.IndexAt(0.2));
            Assert.AreEqual(0, track.IndexAt(0.5));
            Assert.AreEqual(0, track.IndexAt(2.49));
            Assert.AreEqual(-1, track.IndexAt(2.5)); // end es exclusivo
            Assert.AreEqual(1, track.IndexAt(4.0));
            Assert.AreEqual(-1, track.IndexAt(9.0));
        }

        [Test]
        public void Captions_OverlapPicksTheLatestStart()
        {
            const string json = "[{\"start\":0,\"end\":4,\"speaker\":\"\",\"text\":\"A\"},{\"start\":2,\"end\":3,\"speaker\":\"\",\"text\":\"B\"}]";
            CinematicCaptionTrack track = CinematicCaptionTrack.Parse(json);
            Assert.AreEqual("B", track.Lines[track.IndexAt(2.5)].text);
            Assert.AreEqual("A", track.Lines[track.IndexAt(3.5)].text);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("no es json")]
        [TestCase("{\"start\":0}")]
        [TestCase("[{\"start\":0,")]
        [TestCase("[]")]
        public void Captions_BrokenJsonGivesEmptyTrackWithoutThrowing(string json)
        {
            CinematicCaptionTrack track = null;
            Assert.DoesNotThrow(() => track = CinematicCaptionTrack.Parse(json));
            Assert.AreEqual(0, track.Count);
            Assert.AreEqual(-1, track.IndexAt(1.0));
        }

        [Test]
        public void Captions_DropInvalidLines()
        {
            const string json = "[{\"start\":1,\"end\":1,\"text\":\"vacía\"},{\"start\":2,\"end\":1,\"text\":\"al revés\"}," +
                                "{\"start\":-1,\"end\":1,\"text\":\"negativa\"},{\"start\":0,\"end\":1,\"text\":\"  \"}," +
                                "{\"start\":0,\"end\":1,\"text\":\"ok\"}]";
            CinematicCaptionTrack track = CinematicCaptionTrack.Parse(json);
            Assert.AreEqual(1, track.Count);
            Assert.AreEqual("ok", track.Lines[0].text);
        }

        // ---------- Cabecera MP4 ----------

        [Test]
        public void Probe_ReadsVideoTrackSizeAndCodecs()
        {
            byte[] data = BuildFakeMp4(version: 0, audioFirst: true, width: 1920, height: 1080, codec: "avc1");
            CinematicClipProbe.Info info = CinematicClipProbe.Read(data);

            Assert.AreEqual(1920, info.Width);
            Assert.AreEqual(1080, info.Height);
            Assert.IsTrue(info.HasH264);
            Assert.IsTrue(info.HasAac);
            Assert.IsFalse(info.HasHevc);
            CollectionAssert.IsEmpty(CinematicClipProbe.Warnings(info));
        }

        [Test]
        public void Probe_ReadsVersion1Header()
        {
            CinematicClipProbe.Info info = CinematicClipProbe.Read(BuildFakeMp4(version: 1, audioFirst: false, width: 1280, height: 720, codec: "avc1"));
            Assert.AreEqual(1280, info.Width);
            Assert.AreEqual(720, info.Height);
        }

        [Test]
        public void Probe_WarnsOnOddResolutionAndHevc()
        {
            CinematicClipProbe.Info info = CinematicClipProbe.Read(BuildFakeMp4(version: 0, audioFirst: false, width: 1024, height: 1024, codec: "hvc1"));
            List<string> warnings = CinematicClipProbe.Warnings(info);
            Assert.AreEqual(3, warnings.Count, string.Join(" | ", warnings));
        }

        [Test]
        public void Probe_GarbageHasNoResolution()
        {
            CinematicClipProbe.Info info = CinematicClipProbe.Read(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
            Assert.AreEqual(0, info.Width);
            Assert.IsNotEmpty(CinematicClipProbe.Warnings(info));
            Assert.AreEqual(0, CinematicClipProbe.Read(null).Width);
        }

        /// <summary>MP4 mínimo falso: cajas tkhd (audio con ancho 0 y vídeo), y etiquetas de códec.</summary>
        private static byte[] BuildFakeMp4(int version, bool audioFirst, int width, int height, string codec)
        {
            var bytes = new List<byte>();
            if (audioFirst) bytes.AddRange(Tkhd(version, 0, 0));
            bytes.AddRange(Tkhd(version, width, height));
            bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("xxxx" + codec + "yyyymp4azzzz"));
            return bytes.ToArray();
        }

        private static byte[] Tkhd(int version, int width, int height)
        {
            int size = version == 1 ? 104 : 92;
            var box = new byte[size];
            box[0] = 0; box[1] = 0; box[2] = 0; box[3] = (byte)size;
            box[4] = (byte)'t'; box[5] = (byte)'k'; box[6] = (byte)'h'; box[7] = (byte)'d';
            box[8] = (byte)version;
            int w = version == 1 ? 96 : 84;
            box[w] = (byte)(width >> 8); box[w + 1] = (byte)width;
            box[w + 4] = (byte)(height >> 8); box[w + 5] = (byte)height;
            return box;
        }
    }
}
