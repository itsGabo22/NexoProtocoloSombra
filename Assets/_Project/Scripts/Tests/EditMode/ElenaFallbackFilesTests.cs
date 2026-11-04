using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Nexo.AI.Elena;
using NUnit.Framework;
using UnityEngine;

namespace Nexo.Tests
{
    /// <summary>
    /// Todos los Data/ElenaFallback/fallback_floor*.json cumplen el contrato de ElenaResponse (IsValid, como ElenaResponseTests)
    /// y ElenaFallbackProvider los selecciona por número de piso.
    /// </summary>
    [TestFixture]
    public sealed class ElenaFallbackFilesTests
    {
        private static readonly string[] RequiredKeys = { "SAFE_PATH", "OPEN_DOOR", "FEAR", "GENERIC" };

        // Espejo del esquema de ElenaFallbackProvider (sus clases son internas)
        [Serializable]
        public sealed class FallbackEntry
        {
            public int floor;
            public string key;
            public ElenaResponse response;
        }

        [Serializable]
        public sealed class FallbackLibrary
        {
            public FallbackEntry[] entries;
        }

        private static string FallbackFolder => Path.Combine(Application.dataPath, "_Project", "Data", "ElenaFallback");

        private static string[] FallbackFiles()
        {
            return Directory.GetFiles(FallbackFolder, "fallback_floor*.json");
        }

        [Test]
        public void FallbackFiles_ExistForTheThreeFloors()
        {
            var names = new List<string>();
            foreach (string path in FallbackFiles()) names.Add(Path.GetFileName(path));

            CollectionAssert.IsSupersetOf(names, new[] { "fallback_floor12.json", "fallback_floor06.json", "fallback_floor01.json" });
        }

        [Test]
        public void EveryEntry_ParsesAndPassesIsValid_ForEveryIntent()
        {
            foreach (string path in FallbackFiles())
            {
                string file = Path.GetFileName(path);
                FallbackLibrary library = JsonUtility.FromJson<FallbackLibrary>(File.ReadAllText(path));
                Assert.IsNotNull(library?.entries, $"{file}: sin 'entries'.");

                int expectedFloor = int.Parse(Regex.Match(file, @"fallback_floor(\d+)\.json").Groups[1].Value);
                var keys = new List<string>();
                foreach (FallbackEntry entry in library.entries)
                {
                    Assert.AreEqual(expectedFloor, entry.floor, $"{file}: el piso de la entrada {entry.key} no coincide con el archivo.");
                    Assert.IsNotNull(entry.response, $"{file}/{entry.key}: sin 'response'.");
                    Assert.IsTrue(entry.response.IsValid(out string error), $"{file}/{entry.key}: {error}");
                    Assert.LessOrEqual(entry.response.dialogue.Length, ElenaResponse.MaxDialogueLength, $"{file}/{entry.key}: diálogo demasiado largo.");
                    keys.Add(entry.key);
                }

                CollectionAssert.IsSupersetOf(keys, RequiredKeys, $"{file}: faltan intenciones.");
            }
        }

        [Test]
        public void EveryDialogue_HidesTechnicalIds()
        {
            // Los ids técnicos (F12_Door_Stairs, F01_BioScanner…) solo pueden ir en target_id, nunca en lo que lee el jugador
            var technicalId = new Regex(@"\bF\d{2}_[A-Za-z]");
            foreach (string path in FallbackFiles())
            {
                string file = Path.GetFileName(path);
                FallbackLibrary library = JsonUtility.FromJson<FallbackLibrary>(File.ReadAllText(path));
                foreach (FallbackEntry entry in library.entries)
                {
                    Assert.IsFalse(technicalId.IsMatch(entry.response.dialogue), $"{file}/{entry.key}: el diálogo muestra un id técnico.");
                }
            }
        }

        [Test]
        public void Provider_SelectsTheEntryOfEachFloor()
        {
            var assets = new List<TextAsset>();
            var expected = new List<FallbackEntry>();
            foreach (string path in FallbackFiles())
            {
                string json = File.ReadAllText(path);
                assets.Add(new TextAsset(json));
                expected.AddRange(JsonUtility.FromJson<FallbackLibrary>(json).entries);
            }

            var provider = new ElenaFallbackProvider(assets);
            foreach (FallbackEntry entry in expected)
            {
                ElenaResponse response = provider.Get(entry.floor, entry.key);
                Assert.AreEqual(entry.response.dialogue, response.dialogue, $"Piso {entry.floor}/{entry.key}: el proveedor devolvió otra línea.");
            }
        }
    }
}
