using System;
using System.Collections.Generic; // For List<>

namespace DeepSimGames.UnchartedReach.Common.DTOs
{
    [Serializable]
    public class PlayerProfileDto
    {
        public long playerId;
        public string firebaseUid;
        public string email;
        public string displayName;
        // Assuming resourcesJson from backend is now parsed into this list on backend DTO
        // If backend still sends raw JSON string, this needs adjustment
        public List<PlayerResourceDto> resources; // List of resources
        public string createdAt; // Using string, parse later if needed
        public string updatedAt; // Using string, parse later if needed

        public PlayerProfileDto()
        {
            resources = new List<PlayerResourceDto>(); // Initialize list
        }
    }
}