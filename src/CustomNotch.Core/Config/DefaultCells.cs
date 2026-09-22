namespace CustomNotch.Core.Config;

/// <summary>Le cells.json du premier lancement : une pilule à droite, système + un lanceur. Assez pour voir que tout
/// marche, et un modèle à copier.</summary>
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
                { "id": "cpu",  "source": "system.cpu",     "label": "CPU",     "glyph": "cpu",     "refresh": "2s" },
                { "id": "mem",  "source": "system.memory",  "label": "Mémoire", "glyph": "memory",  "refresh": "5s" },
                { "id": "disk", "source": "system.disk",    "label": "Disque",  "glyph": "disk",    "refresh": "1m", "params": { "drive": "C:" } },
                { "id": "net",  "source": "system.network", "label": "Réseau",  "glyph": "network", "refresh": "2s" },
                { "id": "clickup", "source": "launcher", "label": "ClickUp", "glyph": "link", "params": { "open": "https://app.clickup.com" } }
              ]
            }
          ]
        }
        """;
}
