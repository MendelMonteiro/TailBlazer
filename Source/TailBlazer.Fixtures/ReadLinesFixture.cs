﻿using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using TailBlazer.Domain.FileHandling;
using Xunit;

namespace TailBlazer.Fixtures
{
    public class ReadLinesFixture
    {
     //   [Fact()]
        public void ReadFiles()
        {
            var file = Path.GetTempFileName();
            var info = new FileInfo(file);

            //File.AppendAllLines(file, Enumerable.Range(1, 100000)
                
            //    .GroupBy(i=>i % 100==1)

            //    .Select(i => string.Join(",",i)))
            //    ;
                        



            File.AppendAllLines(file, Enumerable.Range(1, 100000).Select(i => $"This is line number {i}"));

            var indexer = new LineIndexer(info);
            var  items = indexer.ReadToEnd().ToArray();

            var actual = File.ReadAllLines(file).ToArray();


            var toRead = Enumerable.Range(0, 1000)
                .Select(i => items.InferPosition(i))
                .ToArray();
                ;

            var largest = toRead.Last();
            var fileLength = file.Length;

            Console.WriteLine();
            var convertedLines = info.ReadLine(toRead, (li, str) =>
            {
                return str;
            }).ToArray();

            File.Delete(file);

        }



        //public class ArrayToLineEnum



        [Fact]
        public void ReadSpecificFileLines()
        {
            var file = Path.GetTempFileName();
            var info = new FileInfo(file);
            File.AppendAllLines(file, Enumerable.Range(1, 100).Select(i => i.ToString()));

            var lines = info.ReadLines(new[] { 1, 2, 3, 10, 100, 105 });

            lines.Select(l=>l.Number).ShouldAllBeEquivalentTo(new[] { 1, 2, 3, 10, 100 });

            File.Delete(file);
        }
    }
}