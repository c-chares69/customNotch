namespace CustomNotch.Core.Model;

/// <summary>L'état d'une cellule, ce que la couleur de l'anneau dit d'un coup d'œil. Busy et Attention viennent
/// d'une source qui sait qu'un travail est en cours ou qu'on attend l'utilisateur (Claude, un timer, une CI).</summary>
public enum Status { Ok, Warn, Crit, Off, Busy, Attention }

/// <summary>Comment la cellule se dessine ; déduit de la lecture quand la config ne le fixe pas.</summary>
public enum CellKind { Ring, Value, Status, Sparkline, Group }
