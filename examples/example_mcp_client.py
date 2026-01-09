#!/usr/bin/env python3
"""
Example MCP Client for SolidAI Add-In

This script demonstrates how to interact with the SolidAI Add-In MCP server
from an external Python client using JSON-RPC over HTTP.

Prerequisites:
    pip install requests

Usage:
    python example_mcp_client.py
"""

import requests
import json
import sys
from typing import Dict, Any


class McpClient:
    """Client for communicating with SolidAI Add-In MCP server"""
    
    def __init__(self, base_url: str = "http://localhost:5000"):
        self.base_url = base_url
        self.request_id = 0
    
    def _call(self, method: str, params: Dict[str, Any]) -> Dict[str, Any]:
        """Make a JSON-RPC call to the MCP server"""
        self.request_id += 1
        
        payload = {
            "jsonrpc": "2.0",
            "method": method,
            "params": params,
            "id": f"req-{self.request_id}"
        }
        
        try:
            response = requests.post(self.base_url, json=payload, timeout=30)
            response.raise_for_status()
            return response.json()
        except requests.exceptions.RequestException as e:
            print(f"Error calling MCP server: {e}")
            sys.exit(1)
    
    def create_part_from_intent(self, intent: str, template: str = "Part", units: str = "MMGS") -> Dict[str, Any]:
        """Create a new part document from intent description"""
        return self._call("create_part_from_intent", {
            "intent": intent,
            "template": template,
            "units": units
        })
    
    def create_rectangular_extrusion(self, width: float, height: float, depth: float, plane: str = "Front") -> Dict[str, Any]:
        """Create a rectangular extrusion"""
        return self._call("create_rectangular_extrusion", {
            "width": width,
            "height": height,
            "depth": depth,
            "plane": plane
        })
    
    def rebuild(self, option: str = "Active") -> Dict[str, Any]:
        """Rebuild the model"""
        return self._call("rebuild", {
            "option": option
        })
    
    def export_step(self, output_path: str, step_version: str = "AP214") -> Dict[str, Any]:
        """Export current document to STEP format"""
        return self._call("export_step", {
            "outputPath": output_path,
            "stepVersion": step_version
        })


def print_response(response: Dict[str, Any], command: str):
    """Pretty print a JSON-RPC response"""
    print(f"\n{'='*60}")
    print(f"Command: {command}")
    print(f"{'='*60}")
    
    if "error" in response:
        print(f"ERROR: {response['error']['message']}")
        if "data" in response["error"]:
            print(f"Details: {response['error']['data']}")
    elif "result" in response:
        result = response["result"]
        if result.get("success"):
            print(f"SUCCESS: {result.get('message', 'Command executed')}")
            if "data" in result:
                print(f"Data: {json.dumps(result['data'], indent=2)}")
        else:
            print(f"FAILED: {result.get('message', 'Unknown error')}")
            if "errorDetails" in result:
                print(f"Details: {result['errorDetails']}")
    
    print(f"{'='*60}\n")


def main():
    """Example workflow demonstrating MCP commands"""
    print("SolidAI Add-In - MCP Client Example")
    print("=" * 60)
    
    client = McpClient()
    
    print("\n[1] Creating part from intent...")
    response = client.create_part_from_intent(
        intent="Create a mounting bracket",
        template="Part",
        units="MMGS"
    )
    print_response(response, "create_part_from_intent")
    
    print("\n[2] Creating rectangular extrusion...")
    response = client.create_rectangular_extrusion(
        width=100,
        height=50,
        depth=10,
        plane="Front"
    )
    print_response(response, "create_rectangular_extrusion")
    
    print("\n[3] Rebuilding model...")
    response = client.rebuild(option="Active")
    print_response(response, "rebuild")
    
    print("\n[4] Exporting to STEP...")
    response = client.export_step(
        output_path="C:\\temp\\example_part.step",
        step_version="AP214"
    )
    print_response(response, "export_step")
    
    print("\nWorkflow Complete!")


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        print("\n\nInterrupted by user")
        sys.exit(0)
    except Exception as e:
        print(f"\n\nUnexpected error: {e}")
        sys.exit(1)
