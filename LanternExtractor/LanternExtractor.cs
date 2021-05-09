﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using LanternExtractor.EQ.Pfs;
using LanternExtractor.EQ.Sound;
using LanternExtractor.EQ.Wld;
using LanternExtractor.Infrastructure.Logger;

namespace LanternExtractor
{
    /// <summary>
    /// The main class in the application
    /// </summary>
    static class LanternExtractor
    {
        /// <summary>
        /// Settings - loaded before extraction
        /// </summary>
        private static Settings _settings;

        /// <summary>
        /// The logger - created before extraction and passed into files for logging
        /// </summary>
        private static ILogger _logger;

        private static List<string> _failedExtractions = new List<string>();

        /// <summary>
        /// Entry point for the application 
        /// </summary>
        /// <param name="args">Run arguments</param>
        static void Main(string[] args)
        {
            _logger = new TextFileLogger("log.txt", LogVerbosity.Info);

            _settings = new Settings("settings.txt", _logger);
            _settings.Initialize();

            // Moved default shortname debug value to debug window in project properties

            if (args.Length != 1)
            {
                _logger.LogInfo("Format: lantern.exe <shortname>");
                return;
            }

            List<string> eqFiles = GetValidEqFiles(args);

            if (eqFiles.Count == 0)
            {
                _logger.LogError("No valid EQ files found for: '" + args[0] + "' at path: " + _settings.EverQuestDirectory);
                return;
            }      
                        
            foreach (var file in eqFiles)
            {
                //_logger.LogInfo("Extracting archive: " + eqFiles[i]);
                //var shortName = eqFiles[i];
                ExtractZone(file);
            }
        }

        /// <summary>
        /// Gets a list of valid EQ archives that aren't OBJ or CHR files
        /// </summary>
        /// <param name="args">The runtime arguments, if supplied</param>
        /// <returns>The list of valid archive names</returns>
        private static List<string> GetValidEqFiles(string[] args)
        {
            var validFiles = new List<string>();

            // Get all files in the EQ directory
            if (args[0].ToLower() == "all")
            {
                List<string> eqFiles = Directory
                    .GetFiles(_settings.EverQuestDirectory, "*" + LanternStrings.PfsFormatExtension).ToList();

                // Prune all object and character files - these will be brought in anyway
                foreach (var file in eqFiles)
                {
                    if (file.EndsWith("_chr" + LanternStrings.PfsFormatExtension) ||
                        file.EndsWith("_obj" + LanternStrings.PfsFormatExtension))
                    {
                        continue;
                    }

                    validFiles.Add(Path.GetFileNameWithoutExtension(file));
                }
            }
            else
            {
                var fileName = args[0];

                if (!fileName.EndsWith(LanternStrings.PfsFormatExtension))
                {
                    fileName += LanternStrings.PfsFormatExtension;
                }

                string path = _settings.EverQuestDirectory + fileName;

                if (File.Exists(path))
                {
                    validFiles.Add(Path.GetFileNameWithoutExtension(path));
                }
            }

            return validFiles;
        }

        /// <summary>
        /// Initializes and extracts content from the archives specified based on settings
        /// </summary>
        /// <param name="shortName">The zone shortname</param>
        private static void ExtractZone(string shortName)
        {
            if (string.IsNullOrEmpty(shortName))
            {
                return;
            }

            // Zone - contains geometry, textures
            if (_settings.ExtractZoneFile)
            {
                ExtractZoneFile(shortName);
            }

            // Objects - contains actual object textures and geometry
            if (_settings.ExtractObjectsFile)
            {
                ExtractObjectsFile(shortName);
            }

            // Characters - contains character model geometry
            if (_settings.ExtractCharactersFile)
            {
                ExtractCharactersFile(shortName);
            }

            if (_settings.ExtractSoundFile)
            {
                ExtractSoundFile(shortName);
            }

            _logger.LogInfo("Extraction complete!");
            _logger.LogInfo("");
        }

        /// <summary>
        /// Initializes and extracts files from the main zone S3D and WLD (if specified)
        /// </summary>
        /// <param name="shortName">The zone shortname</param>
        private static void ExtractZoneFile(string shortName)
        {
            var filePath = _settings.EverQuestDirectory + shortName + LanternStrings.PfsFormatExtension;

            var zoneS3DArchive = new PfsArchive(filePath, _logger);

            if (!zoneS3DArchive.Initialize())
            {
                _logger.LogError("Failed to initialize zone PFS archive!");
                return;
            }

            if (_settings.ExtractWld)
            {
                PfsFile zoneWldFile = zoneS3DArchive.GetFile(shortName + LanternStrings.WldFormatExtension);

                if (zoneWldFile != null)
                {
                    var wld = new WldFile(zoneWldFile, shortName, WldType.Zone, _logger, _settings);

                    if (wld.Initialize())
                    {
                        wld.OutputFiles();
                        zoneS3DArchive.WriteAllFiles(wld.GetMaterialTypes(), "Zone/", true);
                    }
                    else
                    {
                        _logger.LogError("Unable to initialize objects.wld");
                    }
                }

                PfsFile zoneObjectsWldFile = zoneS3DArchive.GetFile("objects" + LanternStrings.WldFormatExtension);

                if (zoneObjectsWldFile != null)
                {
                    var wld = new WldFile(zoneObjectsWldFile, shortName, WldType.ZoneObjects, _logger, _settings);

                    if (wld.Initialize())
                    {
                        wld.OutputFiles();
                    }
                    else
                    {
                        _logger.LogError("Unable to initialize objects.wld");
                    }
                }

                PfsFile zoneLightsFile = zoneS3DArchive.GetFile("lights" + LanternStrings.WldFormatExtension);

                if (zoneLightsFile != null)
                {
                    var wld = new WldFile(zoneLightsFile, shortName, WldType.Lights, _logger, _settings);

                    if (wld.Initialize())
                    {
                        wld.OutputFiles();
                    }
                    else
                    {
                        _logger.LogError("Unable to initialize lights.wld");
                    }
                }
            }
            else
            {
                zoneS3DArchive.WriteAllFiles();
            }
        }

        /// <summary>
        /// Initializes and extracts files from the zone objects S3D and WLD (if specified)
        /// </summary>
        /// <param name="shortName">The zone shortname</param>
        private static void ExtractObjectsFile(string shortName)
        {
            var filePath = _settings.EverQuestDirectory + shortName + "_obj" + LanternStrings.PfsFormatExtension;

            var objectsS3DArchive = new PfsArchive(filePath, _logger);

            if (!objectsS3DArchive.Initialize())
            {
                return;
            }

            if (_settings.ExtractWld)
            {
                PfsFile objectsWldFile =
                    objectsS3DArchive.GetFile(shortName + "_obj" + LanternStrings.WldFormatExtension);

                if (objectsWldFile == null)
                {
                    return;
                }

                var wld = new WldFile(objectsWldFile, shortName, WldType.Objects, _logger, _settings);

                if (wld.Initialize())
                {
                    wld.OutputFiles();
                    objectsS3DArchive.WriteAllFiles(wld.GetMaterialTypes(), "Objects/", true);
                }
                else
                {
                    _logger.LogError("Unable to initialize objects wld");
                }
            }
            else
            {
                objectsS3DArchive.WriteAllFiles();
            }
        }

        /// <summary>
        /// Parses and extracts the character models from the archive
        /// </summary>
        /// <param name="shortName">The zone shortname</param>
        private static void ExtractCharactersFile(string shortName)
        {
            var filePath = _settings.EverQuestDirectory + shortName + "_chr" + LanternStrings.PfsFormatExtension;

            var charactersS3DArchive = new PfsArchive(filePath, _logger);

            if (!charactersS3DArchive.Initialize())
            {
                return;
            }

            if (_settings.ExtractWld)
            {
                PfsFile charactersWldFile =
                    charactersS3DArchive.GetFile(shortName + "_chr" + LanternStrings.WldFormatExtension);

                if (charactersWldFile == null)
                {
                    return;
                }

                var wld = new WldFile(charactersWldFile, shortName, WldType.Characters, _logger, _settings);

                if (wld.Initialize())
                {
                    wld.OutputFiles();
                    charactersS3DArchive.WriteAllFiles(wld.GetMaterialTypes(), "Characters/", true);
                }
                else
                {
                    _logger.LogError("Unable to initialize characters WLD");
                }
            }
            else
            {
                charactersS3DArchive.WriteAllFiles();
            }
        }

        /// <summary>
        /// Parses and extracts the sound and music files for the zone
        /// </summary>
        /// <param name="shortName">The zone shortname</param>
        private static void ExtractSoundFile(string shortName)
        {
            var sounds = new EffSndBnk(_settings.EverQuestDirectory + shortName + "_sndbnk" +
                                       LanternStrings.SoundFormatExtension);
            sounds.Initialize();
            var soundEntries =
                new EffSounds(
                    _settings.EverQuestDirectory + shortName + "_sounds" + LanternStrings.SoundFormatExtension, sounds);
            soundEntries.Initialize();
            soundEntries.ExportSoundData(shortName);
        }
    }
}