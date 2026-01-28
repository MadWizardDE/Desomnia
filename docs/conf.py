# Configuration file for the Sphinx documentation builder.

# -- Project information

project = 'Desomnia'
copyright = '2026, mad0x20wizard'
author = 'mad0x20wizard'

release = '3.0'
version = '3.0.0-alpha'

# -- General configuration

extensions = [
    'sphinx.ext.duration',
    'sphinx.ext.doctest',
    'sphinx.ext.autodoc',
    'sphinx.ext.autosummary',
    'sphinx.ext.intersphinx',
]

intersphinx_mapping = {
    'python': ('https://docs.python.org/3/', None),
    'sphinx': ('https://www.sphinx-doc.org/en/master/', None),
}
intersphinx_disabled_domains = ['std']

templates_path = ['_templates']

# -- Options for HTML output

html_theme = 'sphinx_rtd_theme'

# -- Options for EPUB output
epub_show_urls = 'footnote'

html_static_path = ['_static']

def setup(app):
    app.add_css_file('custom.css')   # Sphinx 1.8+
    # app.add_js_file('custom.js')   # if you want JS too
