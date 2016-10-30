﻿/*
    MIT License

    Copyright (c) 2016 @Boulc (https://github.com/Boulc).

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/
namespace TheGnouCommunity.Tools.Synchronization
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    public class Synchronizer
    {
        private readonly object comparisonSyncLock = new object();

        private readonly string sourcePath;
        private readonly string targetPath;

        private readonly IEnumerable<FileInfoWrapper> sourceFiles;
        private readonly IEnumerable<FileInfoWrapper> targetFiles;

        private Dictionary<string, FileInfoWrapper> relativeSourceFiles;
        private Dictionary<string, FileInfoWrapper> relativeTargetFiles;

        private HashSet<FileInfoWrapper> identicalFiles;
        private HashSet<FileInfoWrapper> differentFiles;
        private HashSet<FileInfoWrapper> missingFiles;
        private HashSet<FileInfoWrapper> extraFiles;
        private List<Tuple<FileInfoWrapper, FileInfoWrapper>> similarFiles;

        private Stopwatch sw = new Stopwatch();
        private TimeSpan? comparisonDuration;

        private readonly bool checkFileLength = false;

        public Synchronizer(string sourcePath, string targetPath)
        {
            this.sourcePath = sourcePath;
            this.targetPath = targetPath;

            this.sourceFiles = FileInfoWrapper.GetFiles(this.sourcePath);
            this.targetFiles = FileInfoWrapper.GetFiles(this.targetPath);
        }

        public IEnumerable<string> SourceFiles
        {
            get
            {
                return this.relativeSourceFiles.Keys;
            }
        }

        public IEnumerable<string> TargetFiles
        {
            get
            {
                return this.relativeTargetFiles.Keys;
            }
        }

        public IEnumerable<string> IdenticalFiles
        {
            get
            {
                return this.identicalFiles.Select(t => t.RelativePath);
            }
        }

        public IEnumerable<string> DifferentFiles
        {
            get
            {
                return this.differentFiles.Select(t => t.RelativePath);
            }
        }

        public IEnumerable<string> MissingFiles
        {
            get
            {
                return this.missingFiles.Select(t => t.RelativePath);
            }
        }

        public IEnumerable<string> ExtraFiles
        {
            get
            {
                return this.extraFiles.Select(t => t.RelativePath);
            }
        }

        public IEnumerable<Tuple<string, string>> SimilarFiles
        {
            get
            {
                return this.similarFiles.Select(t => Tuple.Create(t.Item1.RelativePath, t.Item2.RelativePath));
            }
        }

        public void Run()
        {
            lock (this.comparisonSyncLock)
            {
                Console.WriteLine("Starting comparison...");

                this.comparisonDuration = null;
                this.sw.Restart();

                this.relativeSourceFiles = new Dictionary<string, FileInfoWrapper>();
                this.relativeTargetFiles = targetFiles.ToDictionary(t => t.RelativePath, t => t);

                this.identicalFiles = new HashSet<FileInfoWrapper>();
                this.differentFiles = new HashSet<FileInfoWrapper>();
                this.missingFiles = new HashSet<FileInfoWrapper>();
                this.extraFiles = new HashSet<FileInfoWrapper>(this.relativeTargetFiles.Values);

                foreach (FileInfoWrapper sourceFile in sourceFiles)
                {
                    this.relativeSourceFiles.Add(sourceFile.RelativePath, sourceFile);

                    FileInfoWrapper targetFile;
                    if (this.relativeTargetFiles.TryGetValue(sourceFile.RelativePath, out targetFile))
                    {
                        if (!this.checkFileLength || sourceFile.Info.Length == targetFile.Info.Length)
                        {
                            this.identicalFiles.Add(sourceFile);
                        }
                        else
                        {
                            this.differentFiles.Add(sourceFile);
                        }

                        this.extraFiles.Remove(sourceFile);
                    }
                    else
                    {
                        this.missingFiles.Add(sourceFile);
                    }

                    int n = this.identicalFiles.Count + this.differentFiles.Count + this.missingFiles.Count;
                    if (n % 1000 == 0)
                    {
                        Console.WriteLine($"\t{n}");
                    }
                }

                this.similarFiles = this.missingFiles
                    .SelectMany(
                        t => this.extraFiles
                            .Where(u => u.Info.Name == t.Info.Name)
                            .Where(u => !checkFileLength || u.Info.Length == t.Info.Length)
                            .Select(u => Tuple.Create(t, u)))
                    .ToList();

                this.sw.Stop();
                this.comparisonDuration = this.sw.Elapsed;

                Console.WriteLine($"Comparison run in {sw.ElapsedMilliseconds} ms.");

                this.WriteSummary();
            }
        }

        private void WriteSummary()
        {
            Console.WriteLine("Process summary:");
            Console.WriteLine($"\t- {this.relativeSourceFiles.Count} source files.");
            Console.WriteLine($"\t- {this.relativeTargetFiles.Count} target files.");
            Console.WriteLine($"\t- {this.identicalFiles.Count} identical files.");
            Console.WriteLine($"\t- {this.differentFiles.Count} different files.");
            Console.WriteLine($"\t- {this.missingFiles.Count} missing files.");
            Console.WriteLine($"\t- {this.extraFiles.Count} extra files.");
            Console.WriteLine($"\t- {this.similarFiles.Count} similar files.");
            Console.WriteLine();
        }
    }
}
