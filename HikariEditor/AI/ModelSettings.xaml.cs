using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HikariEditor;

public sealed partial class ModelSettings : UserControl
{
    readonly AIConfig _config;
    readonly ObservableCollection<ModelConfig> _models;
    ModelConfig? _current;          // フォームに表示中（＝編集対象）のモデル
    bool _loading;                  // LoadForm 中の TextChanged を無視するためのガード

    public ModelSettings()
    {
        InitializeComponent();

        _config = AIConfig.Load();
        _models = [.. _config.Models];
        modelsList.ItemsSource = _models;

        // 起動時は使用中（アクティブ）モデルを選択しておく
        ModelConfig? active = _config.ActiveModel;
        if (active is not null)
            modelsList.SelectedItem = active;
        else
            LoadForm();
    }

    void ModelsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        CommitForm();                                // 直前の選択を確定
        _current = modelsList.SelectedItem as ModelConfig;
        LoadForm();
    }

    void NameChanged(object sender, TextChangedEventArgs e)
    {
        // 設定名はリスト表示へ即座に反映したいのでここで書き戻す
        if (_loading || _current is null) return;
        _current.Name = nameBox.Text;
    }

    void AddClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        CommitForm();
        ModelConfig model = new() { Name = "新しいモデル" };
        _models.Add(model);
        modelsList.SelectedItem = model;             // SelectionChanged 経由で LoadForm される
        nameBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
        nameBox.SelectAll();
    }

    void DeleteClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (_current is null) return;
        int idx = _models.IndexOf(_current);
        _models.Remove(_current);
        if (_models.Count > 0)
            modelsList.SelectedIndex = Math.Min(idx, _models.Count - 1);
        else
        {
            _current = null;
            LoadForm();
        }
    }

    // フォームの入力内容を編集対象モデルへ書き戻す。
    void CommitForm()
    {
        if (_current is null) return;
        _current.Name = nameBox.Text;
        _current.Api = apiBox.SelectedIndex switch
        {
            1 => ApiKind.Responses,
            2 => ApiKind.Messages,
            _ => ApiKind.ChatCompletions,
        };
        _current.Endpoint = endpointBox.Text.Trim();
        _current.Model = modelBox.Text.Trim();
        _current.ApiKey = keyBox.Password;
        _current.UseApiKeyHeader = apiKeyHeaderToggle.IsOn;
    }

    // 編集対象モデルの内容をフォームへ反映する。未選択ならフォームを無効化する。
    void LoadForm()
    {
        _loading = true;
        // StackPanel には IsEnabled が無いため各入力欄を個別に切り替える
        bool has = _current is not null;
        nameBox.IsEnabled = has;
        apiBox.IsEnabled = has;
        endpointBox.IsEnabled = has;
        modelBox.IsEnabled = has;
        keyBox.IsEnabled = has;
        apiKeyHeaderToggle.IsEnabled = has;
        deleteButton.IsEnabled = has;

        nameBox.Text = _current?.Name ?? "";
        endpointBox.Text = _current?.Endpoint ?? "";
        modelBox.Text = _current?.Model ?? "";
        keyBox.Password = _current?.ApiKey ?? "";
        apiKeyHeaderToggle.IsOn = _current?.UseApiKeyHeader ?? false;
        apiBox.SelectedIndex = _current?.Api switch
        {
            ApiKind.Responses => 1,
            ApiKind.Messages => 2,
            _ => 0,
        };
        _loading = false;
    }

    // ダイアログの確定時に呼ぶ。フォームを確定し、選択中モデルを使用モデルとして保存する。
    public void Persist()
    {
        CommitForm();
        _config.Models = new List<ModelConfig>(_models);
        _config.ActiveModelId = (modelsList.SelectedItem as ModelConfig)?.Id ?? string.Empty;
        _config.Save();
    }
}
