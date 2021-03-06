﻿using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace RaceControl.Common.Settings
{
    public class VideoDialogLayout : BindableBase, IVideoDialogLayout
    {
        private const string Filename = "RaceControl.layout.json";

        private readonly ILogger _logger;
        private readonly JsonSerializer _serializer;

        private ObservableCollection<VideoDialogInstance> _instances;

        public VideoDialogLayout(ILogger logger, JsonSerializer serializer)
        {
            _logger = logger;
            _serializer = serializer;
        }

        public ObservableCollection<VideoDialogInstance> Instances
        {
            get => _instances ??= new ObservableCollection<VideoDialogInstance>();
            set => SetProperty(ref _instances, value);
        }

        public void Add(IEnumerable<VideoDialogInstance> instances)
        {
            foreach (var instance in instances)
            {
                Instances.Add(instance);
            }
        }

        public void Clear()
        {
            Instances.Clear();
        }

        public void Load()
        {
            _logger.Info("Loading video dialog layout...");

            if (!File.Exists(Filename))
            {
                return;
            }

            try
            {
                using var file = File.OpenText(Filename);
                _serializer.Populate(file, this);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An error occurred while loading video dialog layout.");
            }

            _logger.Info("Done loading video dialog layout.");
        }

        public bool Save()
        {
            _logger.Info("Saving video dialog layout...");

            try
            {
                using var file = File.CreateText(Filename);
                _serializer.Serialize(file, this);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "An error occurred while saving video dialog layout.");

                return false;
            }

            _logger.Info("Done saving video dialog layout.");

            return true;
        }
    }
}