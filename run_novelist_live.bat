@echo off
REM ---------------------------------------------------------------------------
REM run_novelist_live.bat
REM Runs the full Novelist CLI pipeline with the live OpenAI client.
REM ---------------------------------------------------------------------------

REM -------- variables --------------------------------------------------------
set CLI=src\Novelist.Cli
set PROJECT_JSON=src\samples\thedoor.project.json
set OUTDIR=outlines
set DRAFTDIR=drafts
set MODEL=gpt-4o

REM -------- feature toggles --------------------------------------------------
REM Change to true if your project.json sets includePrologue / includeEpilogue.
set INCLUDE_PROLOGUE=false
set INCLUDE_EPILOGUE=false

REM -------- ensure output directory exists -----------------------------------
if not exist "%OUTDIR%" mkdir "%OUTDIR%"

echo --------------------------------------------------------------------------
echo STEP 1: Create outline from %PROJECT_JSON%
echo --------------------------------------------------------------------------
dotnet run --project "%CLI%" -- outline create --project "%PROJECT_JSON%" --output "%OUTDIR%"
if errorlevel 1 goto :error

REM -------- pick newest outline ---------------------------------------------
for /f "delims=" %%f in ('dir /b /o:-d "%OUTDIR%\outline_*.json"') do (
    set "OUTLINE=%OUTDIR%\%%f"
    goto :have_outline
)

echo ERROR: No outline_*.json found.
goto :error

:have_outline
echo Using outline file: %OUTLINE%
echo.

REM -------- STEP 2: Expand premise ------------------------------------------
echo --------------------------------------------------------------------------
echo STEP 2: Expand premise
echo --------------------------------------------------------------------------
dotnet run --project "%CLI%" -- outline expand-premise --outline "%OUTLINE%" --model %MODEL% --live -s
if errorlevel 1 goto :error

REM -------- STEP 3: Define story arc ----------------------------------------
echo --------------------------------------------------------------------------
echo STEP 3: Define story arc
echo --------------------------------------------------------------------------
dotnet run --project "%CLI%" -- outline define-arc --outline "%OUTLINE%" --model %MODEL% --live -s
if errorlevel 1 goto :error

REM -------- STEP 4: Define characters ---------------------------------------
echo --------------------------------------------------------------------------
echo STEP 4: Define characters
echo --------------------------------------------------------------------------
dotnet run --project "%CLI%" -- outline define-characters --outline "%OUTLINE%" --model %MODEL% --live -s
if errorlevel 1 goto :error

REM -------- STEP 5: Define sub-plots ----------------------------------------
echo --------------------------------------------------------------------------
echo STEP 5: Define sub-plots
echo --------------------------------------------------------------------------
dotnet run --project "%CLI%" -- outline define-subplots --outline "%OUTLINE%" --model %MODEL% --live -s
if errorlevel 1 goto :error

REM -------- STEP 6: Expand beats --------------------------------------------
echo --------------------------------------------------------------------------
echo STEP 6: Expand beats
echo --------------------------------------------------------------------------
dotnet run --project "%CLI%" -- outline expand-beats --outline "%OUTLINE%" --model %MODEL% --live -s
if errorlevel 1 goto :error

REM -------- STEP 7: Define structure ----------------------------------------
echo --------------------------------------------------------------------------
echo STEP 7: Define structure
echo --------------------------------------------------------------------------
dotnet run --project "%CLI%" -- outline define-structure --outline "%OUTLINE%" --model %MODEL% --live -s
if errorlevel 1 goto :error

REM -------- STEP 8: Build first-chapter draft --------------------------------
echo --------------------------------------------------------------------------
echo STEP 8: Build Chapter 1 draft
echo --------------------------------------------------------------------------
if not exist "%DRAFTDIR%" mkdir "%DRAFTDIR%"
dotnet run --project "%CLI%" -- outline draft --outline "%OUTLINE%" --output "%DRAFTDIR%" --chapters 1-1 --model %MODEL% --live -s
if errorlevel 1 goto :error

REM -------- STEP 8a: Build prologue (optional) -------------------------------
if /I "%INCLUDE_PROLOGUE%"=="true" (
    echo ----------------------------------------------------------------------
    echo STEP 8a: Build Prologue
    echo ----------------------------------------------------------------------
    dotnet run --project "%CLI%" -- outline draft-prologue --outline "%OUTLINE%" --output "%DRAFTDIR%" --model %MODEL% --live -s
    if errorlevel 1 goto :error
)

REM -------- STEP 8b: Build epilogue (optional) -------------------------------
if /I "%INCLUDE_EPILOGUE%"=="true" (
    echo ----------------------------------------------------------------------
    echo STEP 8b: Build Epilogue
    echo ----------------------------------------------------------------------
    dotnet run --project "%CLI%" -- outline draft-epilogue --outline "%OUTLINE%" --output "%DRAFTDIR%" --model %MODEL% --live -s
    if errorlevel 1 goto :error
)

echo.
echo All steps completed successfully.
echo Final outline: %OUTLINE%
echo Drafts located in: %DRAFTDIR%
goto :eof

:error
echo.
echo *** An error occurred. Aborting.
exit /b 1
