﻿
using System;
#if !UNITY_EDITOR && (NETFX_CORE || NET_4_6 || NET_STANDARD_2_0) && !ENABLE_IL2CPP && !ENABLE_MONO
#if UNITY_WINRT
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
#else
using System.Security.Cryptography;
#endif
using System.Threading.Tasks;
#else
using System.Security.Cryptography;
using System.Threading;
#endif
#if TRILIB_USE_ZIP
#if !UNITY_EDITOR && (NETFX_CORE || NET_4_6 || NET_STANDARD_2_0) && !ENABLE_IL2CPP && !ENABLE_MONO
using System.IO.Compression;
#else
using ICSharpCode.SharpZipLib.Zip;
#endif
#endif
using System.IO;
using UnityEngine;
using System.Text;


namespace TriLib
{
    /// <summary>
    /// Represents an Asset Loader which handles ZIP files (extracting them locally) and has synchronous and asynchronous methods.
    /// If you wish to simply load files from ZIP files without extracting them, use AssetLoader or AssetLoaderAsync instead. 
	/// </summary>
	[Obsolete("AssetLoader and AssetLoaderAsync supports ZIP files when using the LoadFromMemory With Textures, LoadFromFileWithTextures and LoadFromBrowserFilesWithTextures methods. Please use these classes instead.")]
    public class AssetLoaderZip : AssetLoaderBase
    {
        /// <summary>
        /// Loads a <see cref="UnityEngine.GameObject"/> from input filename with defined options.
        /// @warning To ensure your materials will be loaded, don´t remove the material files included in the package.
        /// </summary>
        /// <param name="filename">Filename used to load the <see cref="UnityEngine.GameObject"/>.</param>
        /// <param name="options"><see cref="AssetLoaderOptions"/> used to load the object.</param>
        /// <param name="wrapperGameObject">Use this field to load the new <see cref="UnityEngine.GameObject"/> into referenced <see cref="UnityEngine.GameObject"/>.</param>
        /// <param name="progressCallback">Callback used to retrieve file loading percentage.</param>
        /// <returns>A new <see cref="UnityEngine.GameObject"/>.</returns>
        /// <example>
        /// @code
        /// protected void Awake() {
        ///     GameObject myGameObject;
        ///     try {
        ///         using (var assetLoader = new AssetLoader()) {
        ///             gameObject = assetLoader.LoadFromFile("mymodel.fbx");
        ///         }
        ///     } catch (Exception e) {
        ///         Debug.LogFormat("Unable to load mymodel.fbx. The loader returned: {0}", e);
        ///     }
        /// }
        /// @endcode
        /// </example>
        public GameObject LoadFromFile(string filename, AssetLoaderOptions options = null,
            GameObject wrapperGameObject = null, AssimpInterop.ProgressCallback progressCallback = null)
        {
            var result = LoadFileInternal(filename, options, wrapperGameObject, null, false, progressCallback);
            return result as GameObject;
        }

        /// <summary>
        /// Asynchronously loads a <see cref="UnityEngine.GameObject"/> from input filename with defined options.
        /// @warning To ensure your materials will be loaded, don´t remove the material files included in the package.
        /// </summary>
        /// <param name="filename">Filename used to load the <see cref="UnityEngine.GameObject"/>.</param>
        /// <param name="options"><see cref="AssetLoaderOptions"/> used to load the object.</param>
        /// <param name="wrapperGameObject">Use this field to load the new <see cref="UnityEngine.GameObject"/> into referenced <see cref="UnityEngine.GameObject"/>.</param> 
        /// <param name="onAssetLoaded">The action that will be executed when the <see cref="UnityEngine.GameObject"/> be loaded.</param>
        /// <param name="progressCallback">Callback used to retrieve file loading percentage.</param>
        /// <returns>The created Thread on NET 2.0, otherwise returns the created Task.</returns>
        /// <example>
        /// @code
        /// protected void Awake() {
        ///     GameObject myGameObject;
        ///     try {
        ///         using (var assetLoader = new AssetLoader()) {
        ///             assetLoader.LoadFromFile("mymodel.fbx", null, null, delegate(GameObject loadedGameObject) {
        ///                 Debug.Log("My object '" + loadedGameObject.name +  "' has been loaded!");
        ///             });
        ///         }
        ///     } catch (Exception e) {
        ///         Debug.LogFormat("Unable to load mymodel.fbx. The loader returned: {0}", e);
        ///     }
        /// }
        /// @endcode
        /// </example>
#if !UNITY_EDITOR && (NETFX_CORE || NET_4_6 || NET_STANDARD_2_0) && !ENABLE_IL2CPP && !ENABLE_MONO
        public Task LoadFromFileAsync(string filename, AssetLoaderOptions options, GameObject wrapperGameObject,
            ObjectLoadedHandle onAssetLoaded, AssimpInterop.ProgressCallback progressCallback = null)

        {
            return (Task)LoadFileInternal(filename, options, wrapperGameObject, onAssetLoaded, true, progressCallback);
        }
#else
        public Thread LoadFromFileAsync(string filename, AssetLoaderOptions options = null, GameObject wrapperGameObject = null,
            ObjectLoadedHandle onAssetLoaded = null, AssimpInterop.ProgressCallback progressCallback = null)
        {
            return (Thread)LoadFileInternal(filename, options, wrapperGameObject, onAssetLoaded, true, progressCallback);
        }
#endif

        /// <summary>
        /// Searches inside a path and returns the first path of an asset loadable by TriLib.
        /// </summary>
        /// <param name="path">Path to search for loadable models.</param>
        /// <returns>Path to the loadable model, if any.</returns>
        private static string GetReadableAssetPath(string path)
        {
            var supportedExtensions = GetSupportedFileExtensions();
            foreach (var file in Directory.GetFiles(path))
            {
                var fileExtension = FileUtils.GetFileExtension(file);
                if (supportedExtensions.Contains("*" + fileExtension + ";"))
                {
                    return file;
                }
            }
            foreach (var directory in Directory.GetDirectories(path))
            {
                var assetPath = GetReadableAssetPath(directory);
                if (assetPath != null)
                {
                    return assetPath;
                }
            }
            return null;
        }

#if TRILIB_USE_ZIP
        /// <summary>
        /// Helper function to extract all ZIP file contents to a local folder
        /// </summary>
        /// <param name="zipStream">ZIP file <see cref="System.IO.Stream"/>.</param>
        /// <param name="outFolder">Output folder to extract the ZIP files to.</param>
        private static void UnzipFromStream(Stream zipStream, string outFolder)
        {
#if !UNITY_EDITOR && (NETFX_CORE || NET_4_6 || NET_STANDARD_2_0) && !ENABLE_IL2CPP && !ENABLE_MONO
            var zipFile = new ZipArchive(zipStream, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry zipEntry in zipFile.Entries)
            {
                var zipFileStream = zipEntry.Open();
#else
            var zipFile = new ZipFile(zipStream);
            foreach (ZipEntry zipEntry in zipFile)
            {
                if (!zipEntry.IsFile)
                {
                    continue;
                }
                var zipFileStream = zipFile.GetInputStream(zipEntry);
#endif
                var entryFileName = zipEntry.Name;
                var buffer = new byte[4096];
                var fullZipToPath = Path.Combine(outFolder, entryFileName);
                var directoryName = Path.GetDirectoryName(fullZipToPath);
                if (!string.IsNullOrEmpty(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }
                var fileName = Path.GetFileName(fullZipToPath);
                if (fileName.Length == 0)
                {
                    continue;
                }
                using (var streamWriter = File.Create(fullZipToPath))
                {
#if !UNITY_EDITOR && (NETFX_CORE || NET_4_6 || NET_STANDARD_2_0) && !ENABLE_IL2CPP && !ENABLE_MONO
                    zipFileStream.CopyTo(streamWriter);
#else
                    ICSharpCode.SharpZipLib.Core.StreamUtils.Copy(zipFileStream, streamWriter, buffer);
#endif
                }
            }
        }
#endif

        /// <summary>
        /// Internal method that actually loads the <see cref="UnityEngine.GameObject"/>.
        /// </summary>
        /// <param name="filename">Filename used to load the <see cref="UnityEngine.GameObject"/>.</param>
        /// <param name="assetLoaderOptions"><see cref="AssetLoaderOptions"/> used to load the object.</param>
        /// <param name="wrapperGameObject">Use this field to load the new <see cref="UnityEngine.GameObject"/> into referenced <see cref="UnityEngine.GameObject"/>.</param>
        /// <param name="onAssetLoaded">The action that will be executed when the <see cref="UnityEngine.GameObject"/> be loaded.</param>
        /// <param name="async">Pass <c>true</c> to load the model asynchronously.</param>
        /// <param name="progressCallback">Callback used to retrieve file loading percentage.</param>
        /// <returns>Varies.</returns>
        private object LoadFileInternal(string filename, AssetLoaderOptions assetLoaderOptions = null, GameObject wrapperGameObject = null, ObjectLoadedHandle onAssetLoaded = null, bool async = false, AssimpInterop.ProgressCallback progressCallback = null)
        {
            //Gets the file extension
            var fileExtension = FileUtils.GetFileExtension(filename);

            //Checks if the URL is a ZIP file
            if (fileExtension == ".zip")
            {
#if TRILIB_USE_ZIP
                //Gets the filename hash
                var hash = GetSha256(filename);

                //Gets the folder where the file should be extracted or could be already extracted
                var localFilePath = string.Format("{0}/{1}", Application.persistentDataPath, hash);

                //If file hasn't been extracted yet, extracts all ZIP file data to the destination folder
                if (!Directory.Exists(localFilePath))
                {
                    using (var memoryStream = new MemoryStream(FileUtils.LoadFileData(filename)))
                    {
                        UnzipFromStream(memoryStream, localFilePath);
                    }
                }

                //Gets the first asset loadable by TriLib on the destination folder
                var assetPath = GetReadableAssetPath(localFilePath);
                if (assetPath == null)
                {
                    Debug.LogError("No TriLib readable file could be found on the given directory");
                    return null;
                }

                //Loads the found asset
                if (async)
                {
                    return AsyncLoadFileInternal(assetPath, assetLoaderOptions, wrapperGameObject, onAssetLoaded, progressCallback);
                }
                return SyncLoadFileInternal(assetPath, assetLoaderOptions, wrapperGameObject, progressCallback);
#else
                throw new System.Exception("Please enable TriLib ZIP loading");
#endif
            }
            //If the URL is not a ZIP file, loads the file inside the folder
            if (async)
            {
                return AsyncLoadFileInternal(filename, assetLoaderOptions, wrapperGameObject, onAssetLoaded, progressCallback);
            }
            return SyncLoadFileInternal(filename, assetLoaderOptions, wrapperGameObject, progressCallback);
        }

        /// <summary>
        /// Internally loads a <see cref="UnityEngine.GameObject"/> from input filename with defined options.
        /// </summary>
        /// <param name="filename">Filename used to load the <see cref="UnityEngine.GameObject"/>.</param>
        /// <param name="options"><see cref="AssetLoaderOptions"/> used to load the object.</param>
        /// <param name="wrapperGameObject">Use this field to load the new <see cref="UnityEngine.GameObject"/> into referenced <see cref="UnityEngine.GameObject"/>.</param> 
        /// <param name="progressCallback">Callback used to retrieve file loading percentage.</param>
        /// <returns>A new <see cref="UnityEngine.GameObject"/>.</returns>
        private object SyncLoadFileInternal(string filename, AssetLoaderOptions options, GameObject wrapperGameObject, AssimpInterop.ProgressCallback progressCallback = null)
        {
            var basePath = FileUtils.GetFileDirectory(filename);
            InternalLoadFromFile(filename, basePath, options, wrapperGameObject != null, progressCallback);
            var loadedGameObject = BuildGameObject(options, basePath, wrapperGameObject);
            ReleaseImport();
            return loadedGameObject;
        }

        /// <summary>
        /// Internally asynchronously loads a <see cref="UnityEngine.GameObject"/> from input filename with defined options.
        /// </summary>
        /// <param name="filename">Filename used to load the <see cref="UnityEngine.GameObject"/>.</param>
        /// <param name="options"><see cref="AssetLoaderOptions"/> used to load the object.</param>
        /// <param name="wrapperGameObject">Use this field to load the new <see cref="UnityEngine.GameObject"/> into referenced <see cref="UnityEngine.GameObject"/>.</param>
        /// <param name="onAssetLoaded">The action that will be executed when the <see cref="UnityEngine.GameObject"/> be loaded.</param>
        /// <param name="progressCallback">Callback used to retrieve file loading percentage.</param>
        /// <returns>The created Thread on NET 2.0, otherwise returns the created Task.</returns>
        private object AsyncLoadFileInternal(string filename, AssetLoaderOptions options, GameObject wrapperGameObject, ObjectLoadedHandle onAssetLoaded, AssimpInterop.ProgressCallback progressCallback = null)
        {
            var basePath = FileUtils.GetFileDirectory(filename);
            var usesWrapperGameObject = wrapperGameObject != null;
            return ThreadUtils.RunThread(delegate
                {
                    InternalLoadFromFile(filename, basePath, options, usesWrapperGameObject, progressCallback);
                },
                delegate
                {
                    var loadedGameObject = BuildGameObject(options, basePath, wrapperGameObject);
                    if (onAssetLoaded != null)
                    {
                        onAssetLoaded(loadedGameObject);
                    }
                    ReleaseImport();
                }
            );
        }

        /// <summary>
        /// Returns the hash for a given filename.
        /// </summary>
        /// <param name="localFilename">Filename to be hashed.</param>
        /// <returns>SHA256 hash of given filename.</returns>
        private static string GetSha256(string localFilename)
        {
#if !UNITY_EDITOR && (NETFX_CORE || NET_4_6 || NET_STANDARD_2_0) && !ENABLE_IL2CPP && !ENABLE_MONO
            var input = CryptographicBuffer.ConvertStringToBinary(localFilename,  BinaryStringEncoding.Utf8);
            var hasher = HashAlgorithmProvider.OpenAlgorithm("SHA256");
            var hashed = hasher.HashData(input);
            return CryptographicBuffer.EncodeToHexString(hashed);
#else
            var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(localFilename));
            var hash = new StringBuilder();
            foreach (var b in hashBytes)
            {
                hash.Append(b.ToString("x2"));
            }
            return hash.ToString();
#endif
        }
    }
}
