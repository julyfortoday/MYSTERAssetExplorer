﻿using MYSTERAssetExplorer.Core;
using MYSTERAssetExplorer.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MYSTERAssetExplorer.App
{
    public partial class AssetExplorer : Form
    {
        AssetExplorerApp app;
        NodeViewer viewer;
        PanoBuilder builder;

        public AssetExplorer()
        {
            InitializeComponent();
            previewWindow.InitialImage = Properties.Resources.picture_icon_large;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            var uiContext = new UIContext();
            uiContext.WriteToConsole += WriteToConsole;
            uiContext.ListFiles += FillListView;
            uiContext.PopulateFolders += PopulateFolderExplorer;

            viewer = new NodeViewer();
            viewer.RegisterWithUIContext(uiContext);

            app = new AssetExplorerApp(uiContext);
           // viewer.Launch(app);

            builder = new PanoBuilder(); // to be removed and place within the assetexplorerapp class
        }

        private void openFolder_Click(object sender, EventArgs e)
        {
            OpenFolder();
        }

        private void OpenFolder()
        {
            openFolderDialog.FileName = "Select Folder";
            openFolderDialog.CheckPathExists = true;
            openFolderDialog.ShowReadOnly = false;
            openFolderDialog.ReadOnlyChecked = true;
            openFolderDialog.CheckFileExists = false;
            openFolderDialog.ValidateNames = false;
            openFolderDialog.InitialDirectory = app.GetDataDirectory();

            if (openFolderDialog.ShowDialog() == DialogResult.OK)
            {
                app.SetDataDirectory(openFolderDialog.FileName);
            }
        }

        private void WriteToConsole(Color color, string message)
        {
            Invoke(new Action(() =>
            {
                logOutput.AppendText(message + "\r\n", color);
            }));
        }

        private void FillListView(List<IVirtualFile> files)
        {
            fileExplorer.Items.Clear();

            foreach(var file in files)
            {
                var item = new ListViewItem(file.Name, 1);
                var subItems = new ListViewItem.ListViewSubItem[]
                {
                    //new ListViewItem.ListViewSubItem(item, (file.End - file.Start).ToString()),
                    //new ListViewItem.ListViewSubItem(item, file.Start.ToString())
                };

                item.SubItems.AddRange(subItems);
                fileExplorer.Items.Add(item);
            }
        }

        private void launchNodeViewer_Click(object sender, EventArgs e)
        {
            viewer.Launch(app);
        }

        private void LoadImageToViewer()
        {
            //string[] list = fileListing.SelectedItems.Cast<string>().ToArray();

            //// add path data so the images can be found
            //var nodeDir = ""; //app.GetWorkspace().NodeDir;
            //for(int i = 0; i<list.Length; i++)
            //    list[i] = Path.Combine(nodeDir, list[i]);

            //var imageSet = CubeMapImageSet.FillFromArray(list);
            //viewer.SetNode(imageSet);
            //app.SetCurrentSet(imageSet); // hackiness
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            app.SaveRegistry();
            //todo check if everything went okay
            MessageBox.Show("registry changes have been saved");
        }

        private void aboutButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Utility used to view and extract assets of Myst 3 Exile, and Myst 4 Revelation.\r\nCreated in 2017 by James Thomas");
        }

        private void helpButton_Click(object sender, EventArgs e)
        {
            var message = "You must select a folder containing .m3a and .m4b files.\r\n";
            message += "The app will show assets from whatever data files are available in that folder.\r\n";

            message += "\r\nThe app comes with a registry that maps the various asset files contained within the data files";
            message += " to the various nodes.\r\n";
            message += "\r\nAlterations to this registry can be saved, and loaded in subsequent sessions.\r\n";
            MessageBox.Show(message);
        }

        private void folderExplorer_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TreeNode newSelected = e.Node;
            fileExplorer.Items.Clear();
            IVirtualFolder nodeFolderInfo = (IVirtualFolder)newSelected.Tag;
            ListViewItem.ListViewSubItem[] subItems;
            ListViewItem item = null;

            foreach (IVirtualFolder folder in nodeFolderInfo.SubFolders)
            {
                if (folder == null)
                    continue;

                item = new ListViewItem(folder.Name, 0);
                item.Tag = folder;
                if (folder.Name.CaseInsensitiveContains(app.M4B_FileExtension))
                    item.ImageIndex = (int)FileType.M4B;
                else if (folder.Name.Contains(app.M3A_FileExtension))
                    item.ImageIndex = (int)FileType.M3A;
                subItems = new ListViewItem.ListViewSubItem[]
                {
                    new ListViewItem.ListViewSubItem(item, ""),
                };

                item.SubItems.AddRange(subItems);
                fileExplorer.Items.Add(item);
            }

            foreach (var file in nodeFolderInfo.Files)
            {
                item = new ListViewItem(file.Name, (int) file.ContentDetails.Type);
                item.Tag = file;
                if(file.ContentDetails is ArchiveIndex)
                {
                    var archiveIndex = file.ContentDetails as ArchiveIndex;
                    subItems = new ListViewItem.ListViewSubItem[]
                    {
                        new ListViewItem.ListViewSubItem(item, Utils.GetBytesReadable(archiveIndex.End - archiveIndex.Start)),
                        new ListViewItem.ListViewSubItem(item, "(" + archiveIndex.Start + ", " + archiveIndex.End +")")
                    };
                }
                else if (file.ContentDetails is TiledImage)
                {
                    item.ImageIndex = 9;
                    var tiledImage = file.ContentDetails as TiledImage;
                    subItems = new ListViewItem.ListViewSubItem[]
                    {
                        new ListViewItem.ListViewSubItem(item, ""),
                        new ListViewItem.ListViewSubItem(item, tiledImage.Tiles.Count().ToString() + " images")
                    };
                }
                else
                {
                    subItems = new ListViewItem.ListViewSubItem[0];
                }

                item.SubItems.AddRange(subItems);
                fileExplorer.Items.Add(item);
            }

            //fileExplorer.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        }

        private void PopulateFolderExplorer(List<IVirtualFolder> folders)
        {
            Invoke(new Action(() =>
            {
                folderExplorer.Nodes.Clear();
                foreach (var folder in folders)
                {
                    if (folder != null)
                    {
                        var folderNode = new TreeNode(folder.Name, 0, 1);
                        folderNode.Tag = folder;
                        BuildTreeNode(folderNode, folder.SubFolders);
                        folderExplorer.Nodes.Add(folderNode);
                    }
                }
            }));
        }

        private void BuildTreeNode(TreeNode nodeToAddTo, List<IVirtualFolder> subFolders)
        {
            TreeNode childNode;
            List<IVirtualFolder> subSubFolders;
            int iconIndex = 0;
            int selecedIndex = 0;
            foreach (var subFolder in subFolders)
            {
                if (subFolder == null)
                {
                    continue;
                }

                if (subFolder.Name.CaseInsensitiveContains(app.M4B_FileExtension))
                {
                    iconIndex = (int)FileType.M4B;
                    selecedIndex = iconIndex;
                }
                else if (subFolder.Name.CaseInsensitiveContains(app.M3A_FileExtension))
                {
                    iconIndex = (int)FileType.M3A;
                    selecedIndex = iconIndex;
                }
                else
                {
                    // normal folder
                    iconIndex = 0;
                    selecedIndex = 1;
                }
                childNode = new TreeNode(subFolder.Name, iconIndex, selecedIndex);
                childNode.Tag = subFolder;

                if(subFolder is VirtualFolder)
                {
                    subSubFolders = (subFolder as VirtualFolder).SubFolders;
                    if (subSubFolders.Count != 0)
                    {
                        BuildTreeNode(childNode, subSubFolders);
                    }
                    nodeToAddTo.Nodes.Add(childNode);
                }
            }
        }

        private void extractFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            extractFileDialog.InitialDirectory = app.GetExtractionDirectory();
            extractFileDialog.FileName = "SelectedFolder";
            extractFileDialog.Title = "Save Folder";
            if (extractFileDialog.ShowDialog() == DialogResult.OK)
            {
                var extractionPath = Path.GetDirectoryName(extractFileDialog.FileName);
                app.SetExtractionDirectory(extractionPath);

                if (extractionPath.Length < 1)
                {
                    MessageBox.Show("leave a name in the filename field (doesn't matter what, it won't be used)");
                    return;
                }

                var node = folderExplorer.SelectedNode;
                if (node.Tag is IVirtualFolder)
                {
                    var folder = node.Tag as IVirtualFolder;

                    WriteToConsole(Color.LightBlue, "Extracting folder '" + folder.Name + "' to " + extractionPath);
                    app.ExtractFolder(extractionPath, folder);
                }
            }
        }

        private void extractSelectedFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            extractFileDialog.InitialDirectory = app.GetExtractionDirectory();
            extractFileDialog.FileName = "SelectedFiles";
            extractFileDialog.Title = "Save Files";
            if (extractFileDialog.ShowDialog() == DialogResult.OK)
            {
                var extractionPath = Path.GetDirectoryName(extractFileDialog.FileName);
                app.SetExtractionDirectory(extractionPath);

                if (extractionPath.Length < 1)
                {
                    MessageBox.Show("leave a name in the filename field (doesn't matter what, it won't be used)");
                    return;
                }

                WriteToConsole(Color.LightBlue, "Extracting Selected Files to " + extractionPath);

                List<IVirtualFile> files = new List<IVirtualFile>();

                foreach (ListViewItem item in fileExplorer.SelectedItems)
                {
                    if(item.Tag is IVirtualFile)
                    {
                        files.Add(item.Tag as IVirtualFile);
                    }
                }
                app.ExtractFiles(extractionPath, files);
            }
        }

        private void fileExplorer_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (fileExplorer.SelectedItems.Count == 1)
            {
                ListViewItem selected = fileExplorer.SelectedItems[0];
                if (selected.Tag is VirtualFolder)
                    return;

                if (selected.Tag is IVirtualFile)
                {
                    var file = selected.Tag as IVirtualFile;
                    if (!(file.ContentDetails.Type == FileType.Jpg || 
                        file.ContentDetails.Type == FileType.Zap))
                        return;
                }
                var imageData = GetDataForImageFile(selected);
                SetImageDataIntoPreviewWindow(selected.Text, imageData);
            }
            else
            {
                previewWindow.Image = previewWindow.InitialImage;
            }
        }

        private byte[] GetDataForImageFile(ListViewItem item)
        {
            byte[] imageData = new byte[0];
            if (item.Tag is IVirtualFile)
            {
                var file = item.Tag as IVirtualFile;

                if(file.ContentDetails is TiledImage)
                {
                    WriteToConsole(Color.Yellow, "Assembling Tiled Image: '" + file.Name + "'");
                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    imageData = app.GetDataForFile(file);
                    stopwatch.Stop();
                    WriteToConsole(Color.Green, "'" + file.Name + "' assembled in " + stopwatch.ElapsedMilliseconds + "ms");
                }
                else
                {
                    imageData = app.GetDataForFile(file);
                }

            }
            return imageData;
        }

        private void SetImageDataIntoPreviewWindow(string imageName, byte[] imageData)
        {
            try
            {
                Bitmap bmp = Utils.LoadBitmapFromMemory(imageData);
                previewWindow.Image = bmp;
            }
            catch (Exception ex)
            {
                previewWindow.Image = previewWindow.InitialImage;
                WriteToConsole(Color.Red, "ERROR: '" + imageName + "' could not be shown ( " + ex.Message + " )");
            }
        }

        private void collapseAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            folderExplorer.CollapseAll();
        }

        private void expandAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //folderExplorer.ExpandAll(); // doesn't work well with rev's big data file
        }

        private void folderExplorer_AfterExpand(object sender, TreeViewEventArgs e)
        {
            if(e.Node.ImageIndex == 0)
            e.Node.ImageIndex = 1;
        }

        private void folderExplorer_AfterCollapse(object sender, TreeViewEventArgs e)
        {
            if (e.Node.ImageIndex == 1)
                e.Node.ImageIndex = 0;
        }

        private void FindFileButton_Click(object sender, EventArgs e)
        {
            app.FindFile();
        }
    }
}