﻿using MYSTERAssetExplorer.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MYSTERAssetExplorer.Core;
using MYSTERAssetExplorer.Core.Model;
using System.IO;

namespace MYSTERAssetExplorer.App
{
    public class NodeViewerApp
    {
        public AssetExplorerApp MainApp;

        RegistryManager registryManager;
        RegistryPersistenceService registryPersistence;
        RegistryTreeViewManager treeViewManager;
        CubeMapBuilder panoBuilder = new CubeMapBuilder();

        public string SelectedGame { get; private set; }
        public Node SelectedNode { get; private set; }

        public Action<TreeNode[]> PopulateNodes { get; set; }
        public Action<Node> PopulateImages { get; set; }
        public Action<CubeFaceEnum, IVirtualFile> SetImage { get; set; }
        public Action Launch { get; set; }

        public bool MapTypeColorSelected = true;
        bool showMapTypeOption = false;

        public NodeViewerApp()
        {
            registryManager = new RegistryManager();
            registryPersistence = new RegistryPersistenceService();
            treeViewManager = new RegistryTreeViewManager();
        }

        public void WriteToConsole(Color color, string message)
        {
            MainApp.WriteToConsole(color, message);
        }
        public void SetSelectedNode(Node node)
        {
            SelectedNode = node;
        }
        public void SetSelectedGame(string gameName)
        {
            SelectedGame = gameName;
        }

        public byte[] LookupFileImageData(Node node, string fileName)
        {
            var game = SelectedGame;

            if (game == "Revelation" && (!MapTypeColorSelected))
            {
                fileName += "_depth";
            }
            var fileAddress = new VirtualFileAddress(game, node.Scene, node.Zone, node.Number, fileName);
            var file = FindFile(fileAddress);
            if (file != null)
            {
                var imageData = GetDataForFile(file);
                return imageData;
            }
            return new byte[0];
        }

        public void ExportSelectedNode(string fileSavePath, bool saveSeparately = false, bool sphericalProjection = false)
        {
            ExportCubemap(fileSavePath, SelectedNode, saveSeparately, sphericalProjection);
        }

        public CubemapImages GetCubemapImagesForImageSet(Node node, CubeMapImageSet imageSet)
        {
            var data = new CubemapImages();
            data.Back = Utils.LoadBitmapFromMemory(this.LookupFileImageData(node, imageSet.Back.File));
            data.Bottom = Utils.LoadBitmapFromMemory(this.LookupFileImageData(node, imageSet.Bottom.File));
            data.Front = Utils.LoadBitmapFromMemory(this.LookupFileImageData(node, imageSet.Front.File));
            data.Left = Utils.LoadBitmapFromMemory(this.LookupFileImageData(node, imageSet.Left.File));
            data.Right = Utils.LoadBitmapFromMemory(this.LookupFileImageData(node, imageSet.Right.File));
            data.Top = Utils.LoadBitmapFromMemory(this.LookupFileImageData(node, imageSet.Top.File));
            return data;
        }

        public void ExportCubemap(string fileSavePath, Node node, bool saveSeparately = false, bool exportAsSphericalProjection = false)
        {
            CubeMapImageSet imageSet;
            if (node.CubeMaps.Depth != null)
            {
                imageSet = node.CubeMaps.Depth;
            }
            else
                imageSet = node.CubeMaps.Color;

            CubemapImages images = GetCubemapImagesForImageSet(node, imageSet);

            if(!saveSeparately)
            {
                Bitmap finalImage;
                if (exportAsSphericalProjection)
                {
                    MessageBox.Show("Exporting as spherical projection may take up to around a minute to finish.");
                    finalImage = new SphericalProjectionService().ConstructProjection(images);
                }
                else
                    finalImage = new CubeMapBuilder().ConstructCubemap(images);

                ImageSaveService.Save(fileSavePath, finalImage);

                if(exportAsSphericalProjection)
                {
                    MessageBox.Show("Exporting as spherical projection has completed!");
                }
            }
            else
            {
                // with Revelation images need assembly, which create bitmaps in memory
                // the best output for images manipulated by the program as png files
                // that way they retain detail, without creating output files that are gigantic
                // saving as jpg would be too lossy

                // With Exile the original jpg image data can be saved directly from the original files
                // this avoids un-needed conversion and loss of detail or increase the output file size
                if (SelectedGame == "Exile")
                    ExportCubemapDataDirectly(fileSavePath, node);
                else
                    ExportCubemapAsFaces(fileSavePath, images);
            }
        }

        public void ExportCubemapAsFaces(string fileSavePath, CubemapImages images)
        {
            var filenames = GetCubeFileNames(fileSavePath);
            ImageSaveService.Save(filenames[0], images.Back);
            ImageSaveService.Save(filenames[1], images.Bottom);
            ImageSaveService.Save(filenames[2], images.Front);
            ImageSaveService.Save(filenames[3], images.Left);
            ImageSaveService.Save(filenames[4], images.Right);
            ImageSaveService.Save(filenames[5], images.Top);
        }
        private void ExportCubemapDataDirectly(string fileSavePath, Node node)
        {
            var filenames = GetCubeFileNames(fileSavePath);
            var imageSet = node.CubeMaps.Color;
            ImageSaveService.Save(
            filenames[0],
            this.LookupFileImageData(node, imageSet.Back.File));
            ImageSaveService.Save(
            filenames[1],
            this.LookupFileImageData(node, imageSet.Bottom.File));
            ImageSaveService.Save(
            filenames[2],
            this.LookupFileImageData(node, imageSet.Front.File));
            ImageSaveService.Save(
            filenames[3],
            this.LookupFileImageData(node, imageSet.Left.File));
            ImageSaveService.Save(
            filenames[4],
            this.LookupFileImageData(node, imageSet.Right.File));
            ImageSaveService.Save(
            filenames[5],
            this.LookupFileImageData(node, imageSet.Top.File));
        }
        private string[] GetCubeFileNames(string fileSavePath)
        {
            string[] names = new string[6];
            var tag = "[face]";
            names[0] = fileSavePath.Replace(tag, "[face][back]");
            names[1] = fileSavePath.Replace(tag, "[face][bottom]");
            names[2] = fileSavePath.Replace(tag, "[face][front]");
            names[3] = fileSavePath.Replace(tag, "[face][left]");
            names[4] = fileSavePath.Replace(tag, "[face][right]");
            names[5] = fileSavePath.Replace(tag, "[face][top]");
            return names;
        }

        public void AddNodeToRegistry(GameEnum game, Node node)
        {
            registryManager.AddNode(game, node);
        }

        public void RemoveNodeFromRegistry(GameEnum game, Node node)
        {
            registryManager.RemoveNode(game, node);
        }

        public void RefreshRegistryTree()
        {
            // PopulateNodes is null during construction, so assign the handler here before it's used
            treeViewManager.PopulateNodes += PopulateNodes;
            treeViewManager.RegenTreeView(registryManager.Registry);
        }

        public void LoadDefaultRegistry()
        {
            WriteToConsole(Color.Orange, "Loading Default Registry...");
            var registry = registryPersistence.GetDefaultRegistry();
            registryManager.Registry = registry;
            RefreshRegistryTree();
            WriteToConsole(Color.Green, "Default Registry Loaded Successfully!");
        }

        public void LoadCustomRegistry(string path)
        {
            WriteToConsole(Color.Orange, "Loading Custom Registry...");
            var registry = registryPersistence.LoadRegistryFileFromDisk(path);
            registryManager.Registry = registry;
            RefreshRegistryTree();
            WriteToConsole(Color.Green, "Custom Registry Loaded Successfully!");
        }

        public void SaveCustomRegistry(string selectedGame, string fileName)
        {
            WriteToConsole(Color.Orange, "Saving Custom Registry...");
            registryPersistence.SaveRegistryToDisk(registryManager.Registry, selectedGame, fileName);
            WriteToConsole(Color.Green, "Custom Registry Saved!");
        }

        internal IVirtualFile FindFile(VirtualFileAddress fileAddress)
        {
            return MainApp.FindFile(fileAddress);
        }

        internal byte[] GetDataForFile(IVirtualFile file)
        {
            return MainApp.GetDataForFile(file);
        }

        internal void ReceiveImages(string game, string scene, string zone, List<IVirtualFile> files)
        {
            SelectedNode = new Node();
            SelectedNode.Scene = scene;
            SelectedNode.Zone = zone;

            if (files.Count > 5)
                SelectedNode.CubeMaps.Color.Top.File = files[5].Name;
            if (files.Count > 4)
                SelectedNode.CubeMaps.Color.Right.File = files[4].Name;
            if (files.Count > 3)
                SelectedNode.CubeMaps.Color.Left.File = files[3].Name;
            if (files.Count > 2)
                SelectedNode.CubeMaps.Color.Front.File = files[2].Name;
            if (files.Count > 1)
                SelectedNode.CubeMaps.Color.Bottom.File = files[1].Name;
            if (files.Count > 0)
                SelectedNode.CubeMaps.Color.Back.File = files[0].Name;

            PopulateImages(SelectedNode);
        }

        internal List<Node> GetNodeList()
        {
            var list = new List<Node>();
            list.AddRange(registryManager.Registry.Exile.Nodes);
            list.AddRange(registryManager.Registry.Revelation.Nodes);
            return list;
        }

        // for setting images from here

        //if (files.Count > 0)
        //    SetImage(CubeFaceEnum.Back, files[0]);
        //else if (files.Count > 1)
        //    SetImage(CubeFaceEnum.Bottom, files[1]);
        //else if (files.Count > 2)
        //    SetImage(CubeFaceEnum.Front, files[2]);
        //else if (files.Count > 3)
        //    SetImage(CubeFaceEnum.Left, files[3]);
        //else if (files.Count > 4)
        //    SetImage(CubeFaceEnum.Right, files[4]);
        //else if (files.Count > 5)
        //    SetImage(CubeFaceEnum.Top, files[5]);
    }
}
