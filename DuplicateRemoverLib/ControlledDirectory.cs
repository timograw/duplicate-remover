﻿using Nodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace DuplicateRemoverLib
{
    public class ControlledDirectory
    {
        public string Name { get; private set; }

        public string RootPath { get; private set; }

        public string CacheFilename {
            get
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(RootPath, "DuplicateRemover.cache.gzip");
            }
        }

        private FileStream cacheFile;

        public DirectoryNode RootNode;

        public AbstractProgressManager Progress { get; set; }

        public ControlledDirectory(string name, string rootPath)
        {
            Name = name;
            RootPath = rootPath;
            cacheFile = new FileStream(CacheFilename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        public void Update()
        {
            var scanner = new FilesystemScanner();
            var newRoot = scanner.Scan(RootPath);
            var filesystemCombiner = new FilesystemCombiner();
            var result = filesystemCombiner.Combine(RootNode, newRoot);
            RootNode = newRoot;
        }

        public bool Load()
        {
            // New file
            if (cacheFile.Length <= 0) return false;

            cacheFile.Seek(0, SeekOrigin.Begin);

            using (var stream = new GZipStream(cacheFile, CompressionMode.Decompress, true))
            {
                var formatter = new BinaryFormatter();
                RootNode = (DirectoryNode)formatter.Deserialize(stream);
            }

            return true;
        }

        public void Save()
        {
            cacheFile.Seek(0, SeekOrigin.Begin);
            IFormatter formatter = new BinaryFormatter();
            Directory.CreateDirectory(Path.GetDirectoryName(CacheFilename));
            using (var stream = new GZipStream(cacheFile, CompressionLevel.Fastest, true))
            {
                formatter.Serialize(stream, RootNode);
            }

        }

        public void Hash(int max = 0)
        {
            Progress.Update(0, "");
            Progress.Start();
            var unhashedFiles = RootNode.FilesRecursive.Where(file => file.Hash1K == null).ToList();

            if (max == 0)
                max = unhashedFiles.Count;

            var count = 0;

            foreach (var file in unhashedFiles)
            {
                file.Calculate1kHash();
                count++;

                Progress.Update((double)count / (double)max, file.Name);

                if (count >= max)
                {
                    Progress.Update(1);
                    Progress.Stop();
                    return;
                }
            }

            Progress.Stop();
        }

        public List<List<FileNode>> FindDuplicates()
        {
            var dupliatesQuery = from file in RootNode.FilesRecursive
                                 where file.Hash1K != null
                                 group file by file.Hash1K into duplicates
                                 where duplicates.ToList().Count > 1
                                 select duplicates.ToList();

            return dupliatesQuery.ToList();
        }
    }
}
