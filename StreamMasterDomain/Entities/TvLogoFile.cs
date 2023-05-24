﻿namespace StreamMasterDomain.Entities;

public class TvLogoFile : IconFile
{
    public TvLogoFile()
    {
        //DirectoryLocation = FileDefinitions.TVLogo.DirectoryLocation;
        FileExtension = FileDefinitions.TVLogo.FileExtension;
        SMFileType = FileDefinitions.TVLogo.SMFileType;
    }
}