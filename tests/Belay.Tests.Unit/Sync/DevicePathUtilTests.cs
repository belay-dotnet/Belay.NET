// Copyright 2025 Belay.NET Contributors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Belay.Sync;
using Xunit;

namespace Belay.Tests.Unit.Sync
{
    /// <summary>
    /// Tests for the DevicePathUtil class.
    /// </summary>
    public class DevicePathUtilTests
    {
        [Theory]
        [InlineData("", "/")]
        [InlineData(null, "/")]
        [InlineData("   ", "/")]
        [InlineData("/", "/")]
        [InlineData("\\", "/")]
        [InlineData("test", "/test")]
        [InlineData("/test", "/test")]
        [InlineData("\\test", "/test")]
        [InlineData("test/path", "/test/path")]
        [InlineData("test\\path", "/test/path")]
        [InlineData("/test/path/", "/test/path")]
        [InlineData("\\test\\path\\", "/test/path")]
        [InlineData("//test//path//", "/test/path")]
        [InlineData("test//path", "/test/path")]
        public void NormalizePath_ValidPaths_ReturnsNormalizedPath(string? input, string expected)
        {
            var result = DevicePathUtil.NormalizePath(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("test<file")]
        [InlineData("test>file")]
        [InlineData("test:file")]
        [InlineData("test\"file")]
        [InlineData("test|file")]
        [InlineData("test?file")]
        [InlineData("test*file")]
        [InlineData("test\x00file")]
        [InlineData("test\x1ffile")]
        public void NormalizePath_InvalidCharacters_ThrowsArgumentException(string invalidPath)
        {
            Assert.Throws<ArgumentException>(() => DevicePathUtil.NormalizePath(invalidPath));
        }

        [Theory]
        [InlineData("CON")]
        [InlineData("PRN")]
        [InlineData("AUX")]
        [InlineData("NUL")]
        [InlineData("COM1")]
        [InlineData("LPT1")]
        [InlineData("con.txt")]
        [InlineData("prn.py")]
        public void NormalizePath_ReservedNames_ThrowsArgumentException(string reservedName)
        {
            Assert.Throws<ArgumentException>(() => DevicePathUtil.NormalizePath($"/test/{reservedName}"));
        }

        [Theory]
        [InlineData("test", "path", "/test/path")]
        [InlineData("/test", "path", "/test/path")]
        [InlineData("test/", "/path", "/test/path")]
        [InlineData("/test/", "/path/", "/test/path")]
        [InlineData("", "test", "/test")]
        [InlineData("test", "", "/test")]
        public void Combine_ValidPaths_ReturnsCombinedPath(string path1, string path2, string expected)
        {
            var result = DevicePathUtil.Combine(path1, path2);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Combine_EmptyArray_ReturnsRoot()
        {
            var result = DevicePathUtil.Combine();
            Assert.Equal("/", result);
        }

        [Theory]
        [InlineData("/", "/")]
        [InlineData("/test", "/")]
        [InlineData("/test/path", "/test")]
        [InlineData("/test/path/file.txt", "/test/path")]
        public void GetDirectoryName_ValidPaths_ReturnsDirectoryName(string path, string expected)
        {
            var result = DevicePathUtil.GetDirectoryName(path);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("/", "")]
        [InlineData("/test", "test")]
        [InlineData("/test/path", "path")]
        [InlineData("/test/path/file.txt", "file.txt")]
        public void GetFileName_ValidPaths_ReturnsFileName(string path, string expected)
        {
            var result = DevicePathUtil.GetFileName(path);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("/file.txt", "file")]
        [InlineData("/test/file.py", "file")]
        [InlineData("/test/file.tar.gz", "file.tar")]
        [InlineData("/test/file", "file")]
        [InlineData("/test/.hidden", ".hidden")]
        [InlineData("/test/", "")]
        public void GetFileNameWithoutExtension_ValidPaths_ReturnsNameWithoutExtension(string path, string expected)
        {
            var result = DevicePathUtil.GetFileNameWithoutExtension(path);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("/file.txt", ".txt")]
        [InlineData("/test/file.py", ".py")]
        [InlineData("/test/file.tar.gz", ".gz")]
        [InlineData("/test/file", "")]
        [InlineData("/test/.hidden", "")]
        [InlineData("/test/", "")]
        public void GetExtension_ValidPaths_ReturnsExtension(string path, string expected)
        {
            var result = DevicePathUtil.GetExtension(path);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("/test/file.txt", true)]
        [InlineData("test/file.txt", true)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("test<file", false)]
        [InlineData("CON", false)]
        public void IsValidPath_VariousPaths_ReturnsExpectedResult(string? path, bool expected)
        {
            var result = DevicePathUtil.IsValidPath(path);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("/test/file.txt", "/test", true)]
        [InlineData("/test/subdir/file.txt", "/test", true)]
        [InlineData("/test", "/test", true)]
        [InlineData("/other/file.txt", "/test", false)]
        [InlineData("/test", "/test/subdir", false)]
        [InlineData("/anything", "/", true)]
        public void IsUnderDirectory_VariousPaths_ReturnsExpectedResult(string path, string parentDirectory, bool expected)
        {
            var result = DevicePathUtil.IsUnderDirectory(path, parentDirectory);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("C:\\test\\file.txt", "C:\\base", "/test/file.txt")]
        [InlineData("/home/user/file.txt", "/home/user", "/file.txt")]
        [InlineData("relative/path/file.txt", null, "/relative/path/file.txt")]
        [InlineData("", null, "/")]
        public void FromHostPath_ValidPaths_ReturnsDevicePath(string hostPath, string? baseHostPath, string expected)
        {
            var result = DevicePathUtil.FromHostPath(hostPath, baseHostPath);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToHostPath_ValidDevicePath_ReturnsHostPath()
        {
            var devicePath = "/test/file.txt";
            var baseHostPath = "/base/directory";
            
            var result = DevicePathUtil.ToHostPath(devicePath, baseHostPath);
            var expectedPath = Path.Combine("/base/directory", "test", "file.txt");
            
            Assert.Equal(expectedPath, result);
        }

        [Fact]
        public void ToHostPath_RootDevicePath_ReturnsBaseHostPath()
        {
            var devicePath = "/";
            var baseHostPath = "/base/directory";
            
            var result = DevicePathUtil.ToHostPath(devicePath, baseHostPath);
            
            Assert.Equal(baseHostPath, result);
        }
    }
}