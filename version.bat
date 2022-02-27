@echo off
set VF=PolicyPlus\version.txt
git describe --always >> %VF%

