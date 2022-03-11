/*
FLUID Sociability - a simulation tool for evaluating building designs
Copyright (C) 2022 Human Studio, Inc.

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TriLibCore.General;
using TriLibCore;
using FLUID_Simulator;
using TriLibCore.Fbx;

public class LoadSimulation : MonoBehaviour
{
    public Text text;

    public string ticketIdentifer;
    public string[] defaultCommandLineArguments;
    public FileStream logFileStream;
    SimulationTicket ticket;
    AtlasHandler atlasHandler = null;

    // Start is called before the first frame update
    void Start()
    {
        Camera.main.clearFlags = CameraClearFlags.SolidColor;

        if (Environment.GetCommandLineArgs().Length >= 2)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (argument == "-prod")
                {
                    atlasHandler = new AtlasHandler(true);
                }
                else if (argument.Length == 24)
                {
                    ticketIdentifer = argument;
                }
            }
        }
        if (atlasHandler == null)
        {
            atlasHandler = new AtlasHandler(false);
        }

        Console.WriteLine("Processing ticket with identifier " + ticketIdentifer);
        ticket = atlasHandler.GetTicket(new MongoDB.Bson.ObjectId(ticketIdentifer));

        ticket.version = "1.1.1";

        text.text = ticket.PathToModelFile;

        ticket.eventLog.Clear();

        if (string.IsNullOrEmpty(ticket.Location))
        {
            ticket.Location = "Vancouver(YVR)";
        }

        var assetLoaderOptions = AssetLoader.CreateDefaultLoaderOptions();
        assetLoaderOptions.MarkTexturesNoLongerReadable = false;
        assetLoaderOptions.TextureCompressionQuality = TextureCompressionQuality.NoCompression;

        var webRequest = AssetDownloader.CreateWebRequest(ticket.PathToModelFile);
        AssetDownloader.LoadModelFromUri(
            webRequest,
            OnLoad,
            OnMaterialsLoad,
            OnProgress,
            OnError,
            null,
            assetLoaderOptions,
            null,
            "fbx",
            false
        );
    }

    private void OnError(IContextualizedError obj)
    {
        ticket.eventLog.Add(
            new SimulationEvent(
                SimulationEventType.Error,
                $"An error ocurred while loading your Model: {obj.GetInnerException()}"
            )
        );
        atlasHandler.UpdateTicket(ticket);
        Debug.LogError($"An error ocurred while loading your Model: {obj.GetInnerException()}");
    }

    private void OnProgress(AssetLoaderContext assetLoaderContext, float progress)
    {
        if (progress == 1)
        {
            var fbxDocument = assetLoaderContext.RootModel as FBXDocument;
            if (fbxDocument != null)
            {
                var globalSettings = fbxDocument.GlobalSettings;
                var unitScaleFactor = globalSettings.UnitScaleFactor;
            }

            assetLoaderContext.RootGameObject.transform.SetParent(transform);
            assetLoaderContext.RootGameObject.transform.position = new Vector3(0, 0, 0);
            atlasHandler.UpdateTicket(ticket);
            gameObject
                .GetComponent<StructuredCausalModel>()
                .StartSimulation(atlasHandler, ticket, assetLoaderContext.RootGameObject);
        }
        else if (ticket.modelImportProgress + 0.1f < progress)
        {
            ticket.eventLog.Add(
                new SimulationEvent(SimulationEventType.Information, $"Geometry: {progress * 100}%")
            );
            ticket.modelImportProgress = progress;
            atlasHandler.UpdateTicket(ticket);
        }
        else
        {
            atlasHandler.UpdateTimeStamp();
        }
    }

    private void OnMaterialsLoad(AssetLoaderContext assetLoaderContext) { }

    private void OnLoad(AssetLoaderContext assetLoaderContext)
    {
        ticket.modelImportProgress = 1;
        atlasHandler.UpdateTicket(ticket);
    }
}
