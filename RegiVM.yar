rule RegiVMStaticAnalysis
{
    meta:
        description = "Rule to match RegiVM presence in any file."
        author = "Elliesaur"
        date = "2024-09-18"

    strings:
        $byte_sequence = { 01 1A 4B 15 5F E9 1A 15 49 5E 20 01 00 ED 20 01 03 06 49 01 1D FF }

    condition:
        $byte_sequence
}