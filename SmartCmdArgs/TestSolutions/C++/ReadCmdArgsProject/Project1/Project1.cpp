// Project1.cpp : Definiert den Einstiegspunkt für die Konsolenanwendung.
//

#include "stdafx.h"
#include <fstream>


int main(int argc, char* argv[])
{
	std::ofstream file("../CmdLineArgs.txt");
	if (file.is_open())
	{
		for (int i = 1; i < argc; i++) 
		{
			if (i > 1)
				file << " ";
			file << argv[i];
		}
		file.close();
	}
	return 0;
}

