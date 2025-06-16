using System;

namespace DeepSimGames.UnchartedReach.Common.DTOs
{
    [Serializable]
    public class PlayerResourceDto
    {
        public ResourceType type; // Use the ResourceType enum
        public long amount; // Match backend type (long?)

        public PlayerResourceDto() { }
    }

    // Define the ResourceType enum matching your backend
    // Place this either in its own file or within a relevant common script
    public enum ResourceType
    {
        WATER,
        FOOD,
        ASTERITE, // Your new L1 Structure resource
        LUMINA,   // Your new L1 Energy resource
        STRATOSIUM, // Your new L2 Structure resource
        VOLTARIS    // Your new L3 Energy resource
                    // Add other resources later
    }
}