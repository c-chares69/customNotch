namespace CustomNotch.Core.Config;

/// <summary>Le cells.json du premier lancement : une pilule à droite avec la cellule Claude Code, le groupe
/// Système (ses enfants masqués : ils vivent dans la carte du groupe), la lecture en cours, et un lanceur
/// ClickUp. Tout le reste se règle dans Réglages.</summary>
public static class DefaultCells
{
    public static string Json() => """
        {
          // customNotch - configuration des pilules. Ce fichier est portable : pas de chemin machine, pas de secret.
          // Placeholders : ${env:NAME}, ${secret:name}, ${home}. Surcharge locale : cells.<machine>.json.
          "version": 1,
          "pills": [
            {
              "id": "main",
              "edge": "right",
              "along": 0.5,
              "cells": [
                { "id": "claude", "source": "claude", "label": "Claude", "glyph": "claude" },
                { "id": "sys", "label": "Système", "glyph": "cpu", "children": ["cpu", "mem", "disk", "net", "battery"], "headline": "cpu" },
                { "id": "cpu",     "source": "system.cpu",     "label": "CPU",      "glyph": "cpu",     "refresh": "2s", "visible": false },
                { "id": "mem",     "source": "system.memory",  "label": "Mémoire",  "glyph": "memory",  "refresh": "5s", "visible": false },
                { "id": "disk",    "source": "system.disk",    "label": "Disque",   "glyph": "disk",    "refresh": "1m", "visible": false, "params": { "drive": "C:" } },
                { "id": "net",     "source": "system.network", "label": "Réseau",   "glyph": "network", "refresh": "2s", "visible": false },
                { "id": "battery", "source": "system.battery", "label": "Batterie", "glyph": "battery", "visible": false },
                { "id": "media",   "source": "media",          "label": "Média",    "glyph": "music" },
                { "id": "clickup", "source": "launcher",       "label": "ClickUp",  "glyph": "link", "params": { "open": "https://app.clickup.com" } }
              ]
            }
          ]
        }
        """;
}
