#include "region.h"

bool region_monitoring_enabled = false;

bool region_contains(region_t *region, void* address)
{
	char* region_start = (long)(region->start) + (char*)(region->loaded_address);
    char* region_end = (long)(region->end) + (char*)(region->loaded_address);

    if(region_start <= address && address < region_end)
    {
        return true;
    }
    else
    {
        return false;
    }
}
