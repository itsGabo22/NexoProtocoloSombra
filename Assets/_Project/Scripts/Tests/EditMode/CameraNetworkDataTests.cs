using System.Collections.Generic;
using System.IO;
using Nexo.Interaction;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Nexo.Tests
{
    /// <summary>
    /// La red de cámaras (Data/SecurityCameraNetwork.asset) cubre los tres pisos con ids únicos, cada cámara tiene
    /// fotograma archivado para el enlace diferido y cada piso apunta a una escena que existe.
    /// GetFeeds filtra por piso sin mezclar cámaras de otros pisos.
    /// </summary>
    [TestFixture]
    public sealed class CameraNetworkDataTests
    {
        private const string NetworkPath = "Assets/_Project/Data/SecurityCameraNetwork.asset";

        private SecurityCameraNetwork _network;

        [SetUp]
        public void SetUp()
        {
            _network = AssetDatabase.LoadAssetAtPath<SecurityCameraNetwork>(NetworkPath);
            Assert.IsNotNull(_network, "Falta " + NetworkPath);
        }

        [Test]
        public void Floors_AreTwelveSixAndOne()
        {
            CollectionAssert.AreEqual(new[] { 12, 6, 1 }, _network.Floors);
        }

        [Test]
        public void EveryFloor_HasAtLeastOneCameraAndAnExistingScene()
        {
            var feeds = new List<CameraFeedDefinition>();
            foreach (int floor in _network.Floors)
            {
                _network.GetFeeds(floor, feeds);
                Assert.Greater(feeds.Count, 0, $"El piso {floor} no tiene cámaras");
                foreach (CameraFeedDefinition feed in feeds) Assert.AreEqual(floor, feed.Floor, "GetFeeds mezcló pisos");

                string scene = _network.GetScenePath(floor);
                Assert.IsTrue(File.Exists(scene), $"No existe la escena del piso {floor}: {scene}");
            }
        }

        [Test]
        public void FeedIds_AreUniqueAndLabelled()
        {
            var ids = new HashSet<string>();
            foreach (CameraFeedDefinition feed in _network.Feeds)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(feed.Id), "Cámara sin id");
                Assert.IsFalse(string.IsNullOrWhiteSpace(feed.Label), $"{feed.Id} sin etiqueta");
                Assert.IsTrue(ids.Add(feed.Id), $"Id repetido: {feed.Id}");
            }
        }

        [Test]
        public void EveryFeed_HasArchiveFrameAndTimestamp()
        {
            foreach (CameraFeedDefinition feed in _network.Feeds)
            {
                Assert.IsNotNull(feed.ArchiveFrame, $"{feed.Id} sin fotograma archivado (Nexo/Cámaras/Capturar archivo de la red)");
                StringAssert.IsMatch(@"^\d{2}:\d{2}:\d{2}$", feed.ArchiveTimestamp, $"{feed.Id}: hora de archivo inválida");
            }
        }

        [Test]
        public void GetFeeds_UnknownFloor_ReturnsEmptyAndClearsPreviousResults()
        {
            var feeds = new List<CameraFeedDefinition> { new CameraFeedDefinition() };
            _network.GetFeeds(99, feeds);
            Assert.AreEqual(0, feeds.Count);
            Assert.AreEqual(string.Empty, _network.GetScenePath(99));
        }
    }
}
