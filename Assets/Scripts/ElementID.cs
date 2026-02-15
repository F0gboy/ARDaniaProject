public enum ElementID
{
    None,
    
    // Base elements
    Fire,
    Water,
    Earth,
    Air,
    
    // Combined elements
    Steam,      // Fire + Water -
    Mud,        // Water + Earth
    Lava,       // Fire + Earth -
    Dust,       // Earth + Air -
    Cloud,      // Water + Air -
    Smoke,      // Fire + Air -
    
    // Add more as needed...
}