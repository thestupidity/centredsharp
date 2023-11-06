﻿using System.Numerics;
using CentrED.Client;
using ImGuiNET;
using RadarMap = CentrED.Map.RadarMap;

namespace CentrED.UI.Windows; 

public class MinimapWindow : Window{
    public MinimapWindow(UIManager uiManager) : base(uiManager) { }
    public override string Name => "Minimap";
    public override void Draw() {
        if (!Show) return;

        ImGui.Begin("Minimap", ref _show);
        if (ImGui.Button("Reload")) {
            _mapManager.Client.Send(new RequestRadarMapPacket());
        }
        if (_mapManager.Client.Initialized) {
            var currentPos = ImGui.GetCursorScreenPos();
            var tex = RadarMap.Instance.Texture;
            _uiManager.DrawImage(tex, tex.Bounds);
            if (ImGui.IsMouseHoveringRect(currentPos, new(currentPos.X + tex.Bounds.Width, currentPos.Y + tex.Bounds.Height), true)) {
                if (ImGui.IsWindowFocused() && ImGui.IsMouseDown(ImGuiMouseButton.Left)) {
                    var coords = ImGui.GetMousePos() - currentPos;
                    _mapManager.SetPos((ushort)(coords.X * 8), (ushort)(coords.Y * 8));
                }
            }
            _mapManager.CalculateViewRange(_mapManager.Camera, out var rect);
            var p1 = currentPos + new Vector2(rect.Left / 8, rect.Top / 8);
            var p2 = currentPos + new Vector2(rect.Right / 8, rect.Bottom / 8);
            ImGui.GetWindowDrawList().AddRect(p1, p2, ImGui.GetColorU32(UIManager.Red));
        }
        ImGui.End();
    }
}