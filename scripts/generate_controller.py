"""
Drafts one controller (interface + controller + request/response models) from its
PublicApiSpecs JSON into a staging folder for human review before promotion into
LinnworksAPI/V{1,2}/Controllers/<Name>/.

Usage: python generate_controller.py --controller Orders --version v1

TODO: implement, following the pattern already proven by
linnworks-api-python-main/scripts/generate_schemas.py for the Python client.
"""
