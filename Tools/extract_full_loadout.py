import os
import subprocess
import shutil
import time
import json
import urllib.request

# Configuration
API_KEY = "e1a73d9d631a46a8b7e2b6e37ae30492"
ITEMS_TO_EXTRACT = [
    "2752429099", # Chest
    "2748506263", # Legs
    "1172384181", # Arms
    "4112577340", # Helmet
    "1109145282"  # Class item
]

COLLADA_GEN_EXE = "DestinyColladaGenerator-v1-7-15.exe"
COLLADA_DIR = r"E:\GuardianOS\Tools\ColladaGenerator"
COLLADA_GEN_PATH = os.path.join(COLLADA_DIR, COLLADA_GEN_EXE)
OUTPUT_DIR = os.path.join(COLLADA_DIR, "Output", "DestinyModel0")
CACHE_DIR = r"E:\GuardianOS\Tools\TextureCache"

def download_manifest():
    print("[Manifest] Checking for MobileWorldContent.db...")
    manifest_path = os.path.join(COLLADA_DIR, "MobileWorldContent.db")
    
    if os.path.exists(manifest_path):
        print("[Manifest] Found existing manifest database.")
        return True

    print("[Manifest] Not found. Downloading from Bungie...")
    try:
        # Get Manifest Path
        req = urllib.request.Request("https://www.bungie.net/Platform/Destiny2/Manifest/")
        req.add_header("X-API-Key", API_KEY)
        with urllib.request.urlopen(req) as response:
            data = json.loads(response.read().decode())
        
        content_path = data['Response']['mobileWorldContentPaths']['en']
        download_url = "https://www.bungie.net" + content_path
        print(f"[Manifest] Downloading version: {content_path}")
        
        # Download
        with urllib.request.urlopen(download_url) as response, open(manifest_path, 'wb') as out_file:
            shutil.copyfileobj(response, out_file)
            
        print("[Manifest] Download complete. Database installed.")
        return True
    except Exception as e:
        print(f"[Manifest] Error downloading manifest: {e}")
        return False

def extract_item(item_hash):
    print(f"\n[Extraction] Processing item: {item_hash}...")
    
    # Clean output dir
    if os.path.exists(OUTPUT_DIR):
        try:
            shutil.rmtree(os.path.join(COLLADA_DIR, "Output")) # Clean parent output
        except:
            pass
            
    # Run ColladaGenerator
    try:
        cmd = [COLLADA_GEN_PATH, "-g", item_hash]
        print(f"  -> Running: {' '.join(cmd)}")
        # Run inside the Collada dir so it finds the DB
        subprocess.run(cmd, check=True, cwd=COLLADA_DIR)
    except subprocess.CalledProcessError as e:
        print(f"  -> Error executing tool: {e}")
        return

    # Check output
    if not os.path.exists(OUTPUT_DIR):
        print(f"  -> Warning: No output generated for {item_hash}")
        return

    # Move to Cache
    target_dir = os.path.join(CACHE_DIR, item_hash, "DestinyModel0")
    os.makedirs(target_dir, exist_ok=True)
    
    try:
        count = 0
        for root, dirs, files in os.walk(OUTPUT_DIR):
            for file in files:
                src = os.path.join(root, file)
                dst = os.path.join(target_dir, file)
                shutil.copy2(src, dst)
                count += 1
                
        print(f"  -> Success: {count} files cached for {item_hash}")
        
    except Exception as ex:
        print(f"  -> Error moving files: {ex}")

def main():
    if not os.path.exists(COLLADA_GEN_PATH):
        print("Error: ColladaGenerator EXE not found at " + COLLADA_GEN_PATH)
        return

    print("=== GUARDIAN OS TEXTURE AUTOMATION ===")
    
    if not download_manifest():
        print("Fatal: Could not setup manifest. Aborting.")
        return
    
    print("\nStarting batch extraction...")
    for item in ITEMS_TO_EXTRACT:
        extract_item(item)
        
    print("\n=== COMPLETED ===")
    print("Please refresh the web viewer now.")

if __name__ == "__main__":
    main()
