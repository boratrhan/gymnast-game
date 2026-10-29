using System.Runtime.CompilerServices;

// Lets the simulation tests use internal members (such as GymnastSimulation.StartFlightForTests)
// that are not part of the public API.
[assembly: InternalsVisibleTo("GymnastGame.Simulation.Tests")]
