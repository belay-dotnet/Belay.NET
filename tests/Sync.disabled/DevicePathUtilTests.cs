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

namespace Belay.Tests.Unit.Sync {
    /// <summary>
    /// Tests for the DevicePathUtil class.
    /// </summary>
    public class DevicePathUtilTests {
        [Test]
        [TestCase("", "/")]
        [TestCase(null, "/")]
        [TestCase("   ", "/")]
        [TestCase("/", "/")]
        [TestCase("\\", "/")]
        [TestCase("test", "/test")]
        [TestCase("/test", "/test")]
        [TestCase("\\test", "/test")]
        [TestCase("test/path", "/test/path")]
        [TestCase("test\\path", "/test/path")]
        [TestCase("/test/path/", "/test/path")]
        [TestCase("\\test\\path\\", "/test/path")]
        [TestCase("//test//path//", "/test/path")]
        [TestCase("test//path", "/test/path")]
        public void NormalizePath_ValidPaths_ReturnsNormalizedPath(string? input, string expected) {
            var result = DevicePathUtil.NormalizePath(input);
            Assert.Equal(expected, result);
        }

        [Test]
        [TestCase("test<file")]
        [TestCase("test>file")]
        [TestCase("test:file")]
        [TestCase("test\"file")]
        [TestCase("test|file")]
        [TestCase("test?file")]
        [TestCase("test*file")]
        [TestCase("test\x00file")]
        [TestCase("test\x1ffile")]
        public void NormalizePath_InvalidCharacters_ThrowsArgumentException(string invalidPath) {
            Assert.Throws<ArgumentException>(() => DevicePathUtil.NormalizePath(invalidPath));
        }

        [Test]
        [TestCase("CON")]
        [TestCase("PRN")]
        [TestCase("AUX")]
        [TestCase("NUL")]
        [TestCase("COM1")]
        [TestCase("LPT1")]
        [TestCase("con.txt")]
        [TestCase("prn.py")]
        public void NormalizePath_ReservedNames_ThrowsArgumentException(string reservedName) {
            Assert.Throws<ArgumentException>(() => DevicePathUtil.NormalizePath($"/test/{reservedName}"));
        }

        [Test]
        [TestCase("test", "path", "/test/path")]
        [TestCase("/test", "path", "/test/path")]
        [TestCase("test/", "/path", "/test/path")]
        [TestCase("/test/", "/path/", "/test/path")]
        [TestCase("", "test", "/test")]
        [TestCase("test", "", "/test")]
        public void Combine_ValidPaths_ReturnsCombinedPath(string path1, string path2, string expected) {
            var result = DevicePathUtil.Combine(path1, path2);
            Assert.Equal(expected, result);
        }

        [Test]
        public void Combine_EmptyArray_ReturnsRoot() {
            var result = DevicePathUtil.Combine();
            Assert.Equal("/", result);
        }

        [Test]
        [TestCase("/", "/")]
        [TestCase("/test", "/")]
        [TestCase("/test/path", "/test")]
        [TestCase("/test/path/file.txt", "/test/path")]
        public void GetDirectoryName_ValidPaths_ReturnsDirectoryName(string path, string expected) {
            var result = DevicePathUtil.GetDirectoryName(path);
            Assert.Equal(expected, result);
        }

        [Test]
        [TestCase("/", "")]
        [TestCase("/test", "test")]
        [TestCase("/test/path", "path")]
        [TestCase("/test/path/file.txt", "file.txt")]
        public void GetFileName_ValidPaths_ReturnsFileName(string path, string expected) {
            var result = DevicePathUtil.GetFileName(path);
            Assert.Equal(expected, result);
        }

        [Test]
        [TestCase("/file.txt", "file")]
        [TestCase("/test/file.py", "file")]
        [TestCase("/test/file.tar.gz", "file.tar")]
        [TestCase("/test/file", "file")]
        [TestCase("/test/.hidden", ".hidden")]
        [TestCase("/test/", "")]
        public void GetFileNameWithoutExtension_ValidPaths_ReturnsNameWithoutExtension(string path, string expected) {
            var result = DevicePathUtil.GetFileNameWithoutExtension(path);
            Assert.Equal(expected, result);
        }

        [Test]
        [TestCase("/file.txt", ".txt")]
        [TestCase("/test/file.py", ".py")]
        [TestCase("/test/file.tar.gz", ".gz")]
        [TestCase("/test/file", "")]
        [TestCase("/test/.hidden", "")]
        [TestCase("/test/", "")]
        public void GetExtension_ValidPaths_ReturnsExtension(string path, string expected) {
            var result = DevicePathUtil.GetExtension(path);
            Assert.Equal(expected, result);
        }

        [Test]
        [TestCase("/test/file.txt", true)]
        [TestCase("test/file.txt", true)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("test<file", false)]
        [TestCase("CON", false)]
        public void IsValidPath_VariousPaths_ReturnsExpectedResult(string? path, bool expected) {
            var result = DevicePathUtil.IsValidPath(path);
            Assert.Equal(expected, result);
        }

        [Test]
        [TestCase("/test/file.txt", "/test", true)]
        [TestCase("/test/subdir/file.txt", "/test", true)]
        [TestCase("/test", "/test", true)]
        [TestCase("/other/file.txt", "/test", false)]
        [TestCase("/test", "/test/subdir", false)]
        [TestCase("/anything", "/", true)]
        public void IsUnderDirectory_VariousPaths_ReturnsExpectedResult(string path, string parentDirectory, bool expected) {
            var result = DevicePathUtil.IsUnderDirectory(path, parentDirectory);
            Assert.Equal(expected, result);
        }

        [Test]
        [TestCase("C:\\test\\file.txt", "C:\\base", "/test/file.txt")]
        [TestCase("/home/user/file.txt", "/home/user", "/file.txt")]
        [TestCase("relative/path/file.txt", null, "/relative/path/file.txt")]
        [TestCase("", null, "/")]
        public void FromHostPath_ValidPaths_ReturnsDevicePath(string hostPath, string? baseHostPath, string expected) {
            var result = DevicePathUtil.FromHostPath(hostPath, baseHostPath);
            Assert.Equal(expected, result);
        }

        [Test]
        public void ToHostPath_ValidDevicePath_ReturnsHostPath() {
            var devicePath = "/test/file.txt";
            var baseHostPath = "/base/directory";

            var result = DevicePathUtil.ToHostPath(devicePath, baseHostPath);
            var expectedPath = Path.Combine("/base/directory", "test", "file.txt");

            Assert.Equal(expectedPath, result);
        }

        [Test]
        public void ToHostPath_RootDevicePath_ReturnsBaseHostPath() {
            var devicePath = "/";
            var baseHostPath = "/base/directory";

            var result = DevicePathUtil.ToHostPath(devicePath, baseHostPath);

            Assert.Equal(baseHostPath, result);
        }
    }
}
